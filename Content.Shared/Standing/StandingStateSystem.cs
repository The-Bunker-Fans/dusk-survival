using Content.Shared.Hands.Components;
using Content.Shared.Physics;
using Content.Shared.Rotation;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Standing
{
    public sealed class StandingStateSystem : EntitySystem
    {
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly FixtureSystem _fixtureSystem = default!;

        // If StandingCollisionLayer value is ever changed to more than one layer, the logic needs to be edited.
        private const int StandingCollisionLayer = (int) CollisionGroup.MidImpassable;
        private const string StandingSensorFixtureName = "standing-sensor";

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<StandingStateComponent, EndCollideEvent>(OnStandingEndCollide);
        }

        public bool IsDown(EntityUid uid, StandingStateComponent? standingState = null)
        {
            if (!Resolve(uid, ref standingState, false))
                return false;

            return !standingState.Standing;
        }

        public bool Down(EntityUid uid, bool playSound = true, bool dropHeldItems = true,
            StandingStateComponent? standingState = null,
            AppearanceComponent? appearance = null,
            HandsComponent? hands = null)
        {
            // TODO: This should actually log missing comps...
            if (!Resolve(uid, ref standingState, false))
                return false;

            // Optional component.
            Resolve(uid, ref appearance, ref hands, false);

            if (!standingState.Standing)
                return true;

            // This is just to avoid most callers doing this manually saving boilerplate
            // 99% of the time you'll want to drop items but in some scenarios (e.g. buckling) you don't want to.
            // We do this BEFORE downing because something like buckle may be blocking downing but we want to drop hand items anyway
            // and ultimately this is just to avoid boilerplate in Down callers + keep their behavior consistent.
            if (dropHeldItems && hands != null)
            {
                RaiseLocalEvent(uid, new DropHandItemsEvent(), false);
            }

            var msg = new DownAttemptEvent();
            RaiseLocalEvent(uid, msg, false);

            if (msg.Cancelled)
                return false;

            standingState.Standing = false;
            Dirty(uid, standingState);
            RaiseLocalEvent(uid, new DownedEvent(), false);

            // Seemed like the best place to put it
            _appearance.SetData(uid, RotationVisuals.RotationState, RotationState.Horizontal, appearance);

            // Change collision masks to allow going under certain entities like flaps and tables
            if (TryComp(uid, out FixturesComponent? fixtureComponent))
            {
                foreach (var (key, fixture) in fixtureComponent.Fixtures)
                {
                    if ((fixture.CollisionMask & StandingCollisionLayer) == 0 || fixture.Hard == false)
                        continue;

                    standingState.ChangedFixtures.Add(key);
                    _physics.SetCollisionMask(uid, key, fixture, fixture.CollisionMask & ~StandingCollisionLayer, manager: fixtureComponent);
                }

                // Create a temporary sensor to continue registering contacts in the disabled collision layer
                _fixtureSystem.TryCreateFixture(
                    uid,
                    new PhysShapeCircle(0.1f),
                    StandingSensorFixtureName,
                    collisionLayer: (int) CollisionGroup.None,
                    collisionMask: StandingCollisionLayer,
                    hard: false,
                    manager: fixtureComponent);
            }

            // check if component was just added or streamed to client
            // if true, no need to play sound - mob was down before player could seen that
            if (standingState.LifeStage <= ComponentLifeStage.Starting)
                return true;

            if (playSound)
            {
                _audio.PlayPredicted(standingState.DownSound, uid, uid);
            }

            return true;
        }

        public bool Stand(EntityUid uid,
            StandingStateComponent? standingState = null,
            AppearanceComponent? appearance = null,
            bool force = false)
        {
            // TODO: This should actually log missing comps...
            if (!Resolve(uid, ref standingState, false))
                return false;

            // Optional component.
            Resolve(uid, ref appearance, false);

            if (standingState.Standing)
                return true;

            if (!force)
            {
                var msg = new StandAttemptEvent();
                RaiseLocalEvent(uid, msg, false);

                if (msg.Cancelled)
                    return false;
            }

            standingState.Standing = true;
            Dirty(uid, standingState);
            RaiseLocalEvent(uid, new StoodEvent(), false);

            _appearance.SetData(uid, RotationVisuals.RotationState, RotationState.Vertical, appearance);

            if (TryComp(uid, out FixturesComponent? fixtureComponent))
            {
                if (!HasStandingCollisionLayerContacts(uid, fixtureComponent))
                {
                    RestoreChangedFixtures(uid, standingState, fixtureComponent);
                }
            }

            return true;
        }

        private static bool HasStandingCollisionLayerContacts(EntityUid uid, FixturesComponent fixtureComponent)
        {
            if (fixtureComponent.Fixtures.TryGetValue(StandingSensorFixtureName, out var sensor))
            {
                foreach (var contact in sensor.Contacts.Values)
                {
                    var otherFixture = contact.EntityA == uid ? contact.FixtureB : contact.FixtureA;
                    if (otherFixture?.Hard == true)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnStandingEndCollide(EntityUid uid, StandingStateComponent standingState, ref EndCollideEvent args)
        {
            if (args.OurFixtureId != StandingSensorFixtureName || !standingState.Standing)
                return;

            foreach (var otherFixture in args.OurFixture.Contacts.Keys)
            {
                if (otherFixture == args.OtherFixture)
                    continue;

                // If the sensor still colliding with something else, do not restore fixtures
                if (otherFixture.Hard)
                    return;
            }

            RestoreChangedFixtures(uid, standingState);
        }

        private void RestoreChangedFixtures(EntityUid uid, StandingStateComponent? standingState = null, FixturesComponent? fixtures = null)
        {
            if (!Resolve(uid, ref standingState, ref fixtures, false))
                return;

            foreach (var key in standingState.ChangedFixtures)
            {
                if (fixtures.Fixtures.TryGetValue(key, out var fixture))
                    _physics.SetCollisionMask(uid, key, fixture, fixture.CollisionMask | StandingCollisionLayer, fixtures);
            }
            standingState.ChangedFixtures.Clear();
            _fixtureSystem.DestroyFixture(uid, StandingSensorFixtureName, manager: fixtures);
        }
    }

    public sealed class DropHandItemsEvent : EventArgs
    {
    }

    /// <summary>
    /// Subscribe if you can potentially block a down attempt.
    /// </summary>
    public sealed class DownAttemptEvent : CancellableEntityEventArgs
    {
    }

    /// <summary>
    /// Subscribe if you can potentially block a stand attempt.
    /// </summary>
    public sealed class StandAttemptEvent : CancellableEntityEventArgs
    {
    }

    /// <summary>
    /// Raised when an entity becomes standing
    /// </summary>
    public sealed class StoodEvent : EntityEventArgs
    {
    }

    /// <summary>
    /// Raised when an entity is not standing
    /// </summary>
    public sealed class DownedEvent : EntityEventArgs
    {
    }
}
