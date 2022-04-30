using Content.Server.Body.Components;
using Content.Server.Cloning;
using Content.Server.Construction;
using Content.Server.DoAfter;
using Content.Server.Mind.Components;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Shared.CharacterAppearance.Systems;
using Content.Shared.Preferences;
using Content.Shared.Species;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Body.Systems
{
    public sealed class SkeletonBodyManagerSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entities = default!;
        [Dependency] private readonly IServerPreferencesManager _prefsManager = null!;
        [Dependency] private readonly IPrototypeManager _prototype = default!;
        [Dependency] private readonly PopupSystem _popupSystem= default!;
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly ConstructionSystem _construction = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SkeletonBodyManagerComponent, GetVerbsEvent<AlternativeVerb>>(AddReassembleVerbs);
            SubscribeLocalEvent<SkeletonBodyManagerComponent, BeingGibbedEvent>(OnBeingGibbed);
            SubscribeLocalEvent<ReassembleDoAfterComplete>(Reassemble);
        }

        /// <summary>
        /// Adds the custom verb for reassembling skeleton parts
        /// into a full skeleton
        /// </summary>
        private void AddReassembleVerbs(EntityUid uid, SkeletonBodyManagerComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanAccess || !args.CanInteract)
                return;

            if (!TryComp<ActorComponent?>(args.User, out var actor))
                return;

            if (!TryComp<MindComponent>(uid, out var mind) || !mind.HasMind)
                return;

            var doAfterTime = component.DoAfterTime;

            // doubles the time if you reconstruct yourself
            if (args.User == uid)
                doAfterTime *= 2;

            var doAfterEventArgs = new DoAfterEventArgs(component.Owner, doAfterTime, default, component.Owner)
            {
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                BreakOnDamage = true,
                BreakOnStun = true,
                NeedHand = false,
                BroadcastFinishedEvent = new ReassembleDoAfterComplete(uid, args.User, component),
            };

            // Custom verb
            AlternativeVerb custom = new();
            custom.Text = Loc.GetString("skeleton-reassemble-action");
            custom.Act = () => _doAfterSystem.DoAfter(doAfterEventArgs);
            custom.IconTexture = "/Textures/Mobs/Species/Skeleton/parts.rsi/full.png";
            custom.Priority = 1;
            args.Verbs.Add(custom);
        }

        private void Reassemble(ReassembleDoAfterComplete args)
        {
            var uid = args.Uid;
            var component = args.Component;

            if (component.DNA == null || component.BodyParts == null)
                return;

            // Ensures all of the old body part pieces are there
            var nearby = _construction.EnumerateNearby(args.User);

            // there is certainly a less stinky way of doing this but alas
            // i am a little baby coder who's only taken intro classes. -emo
            var foundBodyParts = new List<EntityUid>();
            foreach (var entity in nearby)
            {
                foreach (var target in component.BodyParts)
                {
                    if (target.Owner == entity)
                    {
                        //checking code will run somewhere here
                        // please recommend a way to do this in a sane way
                    }
                }
            }

            // Creates the new entity and transfers the mind component
            var speciesProto = _prototype.Index<SpeciesPrototype>(component.DNA.Value.Profile.Species).Prototype;
            var mob = _entities.SpawnEntity(speciesProto, _entities.GetComponent<TransformComponent>(component.Owner).MapPosition);

            Get<SharedHumanoidAppearanceSystem>().UpdateFromProfile(mob, component.DNA.Value.Profile);
            _entities.GetComponent<MetaDataComponent>(mob).EntityName = component.DNA.Value.Profile.Name;

            if (TryComp<MindComponent>(uid, out var mindcomp) && mindcomp.Mind != null)
                mindcomp.Mind.TransferTo(mob);

            // Cleans up all the body part pieces
            foreach(var entity in foundBodyParts)
            {
                _entities.DeleteEntity(entity);
            }
        }

        /// <summary>
        /// Called before the skeleton entity is gibbed in order to save
        /// the dna for reassembly later
        /// </summary>
        /// <param name="uid"></param> the entity the mind is going to be transfered which also stores the DNA
        /// <param name="body"></param> the entity whose DNA is being saved
        public void UpdateDNAEntry(EntityUid uid, EntityUid body)
        {
            if (!TryComp<SkeletonBodyManagerComponent>(uid, out var skelBodyComp) ||
                !TryComp<MindComponent>(body, out var mindcomp))
                return;

            if (mindcomp.Mind == null)
                return;

            if (mindcomp.Mind.UserId == null)
                return;

            var profile = (HumanoidCharacterProfile) _prefsManager.GetPreferences(mindcomp.Mind.UserId.Value).SelectedCharacter;
            skelBodyComp.DNA = new ClonerDNAEntry(mindcomp.Mind, profile);
        }

        /// gets the list of body parts that were dropped when the entity was gibbed.
        private void OnBeingGibbed(EntityUid uid, SkeletonBodyManagerComponent component, BeingGibbedEvent args)
        {
            component.BodyParts = args.GibbedParts;
        }

        private sealed class ReassembleDoAfterComplete : EntityEventArgs
        {
            public readonly EntityUid Uid; //the entity being reassembled
            public readonly EntityUid User; //the user performing the reassembly
            public readonly SkeletonBodyManagerComponent Component;

            public ReassembleDoAfterComplete(EntityUid uid, EntityUid user, SkeletonBodyManagerComponent component)
            {
                Uid = uid;
                User = user;
                Component = component;
            }
        }
    }
}
