using System.Linq;
using Content.Server.Popups;
using Content.Server.Tabletop.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Tabletop;
using Content.Shared.Tabletop.Components;
using Content.Shared.Tabletop.Events;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.Tabletop
{
    [UsedImplicitly]
    public sealed partial class TabletopSystem : SharedTabletopSystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly ViewSubscriberSystem _viewSubscriberSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<TabletopStopPlayingEvent>(OnStopPlaying);
            SubscribeLocalEvent<TabletopGameComponent, ActivateInWorldEvent>(OnTabletopActivate);
            SubscribeLocalEvent<TabletopGameComponent, ComponentShutdown>(OnGameShutdown);
            SubscribeLocalEvent<TabletopGamerComponent, PlayerDetachedEvent>(OnPlayerDetached);
            SubscribeLocalEvent<TabletopGamerComponent, ComponentShutdown>(OnGamerShutdown);
            SubscribeLocalEvent<TabletopGameComponent, GetVerbsEvent<ActivationVerb>>(AddPlayGameVerb);
            SubscribeLocalEvent<TabletopGameComponent, InteractUsingEvent>(OnInteractUsing);
            SubscribeLocalEvent<TabletopGameComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);

            SubscribeNetworkEvent<TabletopRequestTakeOut>(OnTabletopRequestTakeOut);

            InitializeMap();
        }

        /// <summary>
        /// Dumps all entities inside of the tabletop component out.
        /// </summary>
        /// <param name="tableUid">The tabletop component in question</param>
        /// <param name="dumpTabletopPieces">Should we also dump out the pieces with the "TabletopPiece" tag?</param>
        private bool DumpAllPiecesOut(EntityUid tableUid, bool dumpTabletopPieces = false)
        {
            if (!TryComp(tableUid, out TabletopGameComponent? tabletop) || tabletop.Session is not { } session)
                return false;

            var piecesDumped = false;

            foreach (var entity in session.Entities.ToList())
            {
                if (TryComp<TagComponent>(entity, out var tag))
                {
                    if ((tag.Tags.Contains("TabletopPiece") && !dumpTabletopPieces) || tag.Tags.Contains("TabletopBoard"))
                        continue;
                }

                // Find the entity, remove it from the session and set it's position to the tabletop
                session.Entities.Remove(entity);
                RemComp<TabletopDraggableComponent>(entity);

                // Get the transform of the object so that we can manipulate it
                var xform = Transform(tableUid);
                _transformSystem.SetParent(entity, _mapManager.GetMapEntityId(xform.MapID));
                _transformSystem.SetWorldPosition(entity, xform.MapPosition.Position);

                piecesDumped = true;
            }

            return piecesDumped;
        }

        private void OnPickupAttempt(EntityUid uid, TabletopGameComponent component, GettingPickedUpAttemptEvent args)
        {
            if (!component.DumpPiecesOnPickup)
                return;

            var dumped = DumpAllPiecesOut(uid);
            if (dumped)
                _popupSystem.PopupEntity(Loc.GetString("tabletop-pieces-fell"), uid, PopupType.Large);
        }

        private void OnTabletopRequestTakeOut(TabletopRequestTakeOut msg, EntitySessionEventArgs args)
        {
            if (args.SenderSession is not IPlayerSession playerSession)
                return;

            if (!TryComp(msg.TableUid, out TabletopGameComponent? tabletop) || tabletop.Session is not { } session)
                return;

            if (!msg.Entity.IsValid())
                return;

            // Check if player is actually playing at this table
            if (!session.Players.ContainsKey(playerSession))
                return;

            // Find the entity, remove it from the session and set it's position to the tabletop
            session.Entities.TryGetValue(msg.Entity, out var result);
            session.Entities.Remove(result);
            RemComp<TabletopDraggableComponent>(result);

            // Get the transform of the object so that we can manipulate it
            var xform = Transform(msg.TableUid);
            _transformSystem.SetParent(result, _mapManager.GetMapEntityId(xform.MapID));
            _transformSystem.SetWorldPosition(msg.Entity, xform.MapPosition.Position);
        }

        private void OnInteractUsing(EntityUid uid, TabletopGameComponent component, InteractUsingEvent args)
        {
            if (!EntityManager.TryGetComponent(args.User, out HandsComponent? hands))
                return;

            if (component.Session is not { } session)
                return;

            if (hands.ActiveHand == null)
                return;

            if (hands.ActiveHand.HeldEntity == null)
                return;

            var handEnt = hands.ActiveHand.HeldEntity.Value;

            if (!TryComp<ItemComponent>(handEnt, out var item))
                return;

            if (component.Blacklist != null && component.Blacklist.IsValid(handEnt))
            {
                _popupSystem.PopupEntity(Loc.GetString("tabletop-too-big"), uid);
                return;
            }

            if (item.Size > component.PieceMaxSize)
            {
                _popupSystem.PopupEntity(Loc.GetString("tabletop-too-big"), uid);
                return;
            }

            // guess i had to do parenting bullshit after all
            // Make sure the entity can be dragged, move it into the board game world and add it to the Entities hashmap
            _transformSystem.SetWorldPosition(handEnt, session.Position.Offset(-1, 0).Position);
            _transformSystem.SetParent(handEnt, _mapManager.GetMapEntityId(session.Position.MapId));
            _transformSystem.SetWorldRotation(handEnt, new Angle(0));
            EnsureComp<TabletopDraggableComponent>(handEnt);
            session.Entities.Add(handEnt);
        }

        protected override void OnTabletopMove(TabletopMoveEvent msg, EntitySessionEventArgs args)
        {
            if (args.SenderSession is not IPlayerSession playerSession)
                return;

            if (!TryComp(msg.TableUid, out TabletopGameComponent? tabletop) || tabletop.Session is not { } session)
                return;

            // Check if player is actually playing at this table
            if (!session.Players.ContainsKey(playerSession))
                return;

            base.OnTabletopMove(msg, args);
        }

        /// <summary>
        /// Add a verb that allows the player to start playing a tabletop game.
        /// </summary>
        private void AddPlayGameVerb(EntityUid uid, TabletopGameComponent component, GetVerbsEvent<ActivationVerb> args)
        {
            if (!args.CanAccess || !args.CanInteract)
                return;

            if (!EntityManager.TryGetComponent<ActorComponent?>(args.User, out var actor))
                return;

            ActivationVerb playVerb = new()
            {
                Text = Loc.GetString("tabletop-verb-play-game"),
                Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/die.svg.192dpi.png")),
                Act = () => OpenSessionFor(actor.PlayerSession, uid)
            };

            ActivationVerb dumpVerb = new()
            {
                Act = () =>
                {
                    var dumped = DumpAllPiecesOut(uid, true);
                    if (dumped)
                        _popupSystem.PopupEntity(Loc.GetString("tabletop-pieces-fell"), uid, PopupType.Large);
                },
                Text = Loc.GetString("tabletop-verb-dump-pieces"),
                Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/close.svg.192dpi.png"))
            };

            args.Verbs.Add(playVerb);
            args.Verbs.Add(dumpVerb);
        }

        private void OnTabletopActivate(EntityUid uid, TabletopGameComponent component, ActivateInWorldEvent args)
        {
            // Check that a player is attached to the entity.
            if (!EntityManager.TryGetComponent(args.User, out ActorComponent? actor))
                return;

            OpenSessionFor(actor.PlayerSession, uid);
        }

        private void OnGameShutdown(EntityUid uid, TabletopGameComponent component, ComponentShutdown args)
        {
            CleanupSession(uid);
        }

        private void OnStopPlaying(TabletopStopPlayingEvent msg, EntitySessionEventArgs args)
        {
            CloseSessionFor((IPlayerSession)args.SenderSession, msg.TableUid);
        }

        private void OnPlayerDetached(EntityUid uid, TabletopGamerComponent component, PlayerDetachedEvent args)
        {
            if(component.Tabletop.IsValid())
                CloseSessionFor(args.Player, component.Tabletop);
        }

        private void OnGamerShutdown(EntityUid uid, TabletopGamerComponent component, ComponentShutdown args)
        {
            if (!EntityManager.TryGetComponent(uid, out ActorComponent? actor))
                return;

            if(component.Tabletop.IsValid())
                CloseSessionFor(actor.PlayerSession, component.Tabletop);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var gamer in EntityManager.EntityQuery<TabletopGamerComponent>())
            {
                if (!EntityManager.EntityExists(gamer.Tabletop))
                    continue;

                if (!EntityManager.TryGetComponent(gamer.Owner, out ActorComponent? actor))
                {
                    EntityManager.RemoveComponent<TabletopGamerComponent>(gamer.Owner);
                    return;
                }

                var gamerUid = (gamer).Owner;

                if (actor.PlayerSession.Status != SessionStatus.InGame || !CanSeeTable(gamerUid, gamer.Tabletop))
                    CloseSessionFor(actor.PlayerSession, gamer.Tabletop);
            }
        }
    }
}
