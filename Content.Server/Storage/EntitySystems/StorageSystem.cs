using System.Collections.Generic;
using Content.Server.Interaction;
using Content.Server.Storage.Components;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server.Storage.EntitySystems
{
    [UsedImplicitly]
    internal sealed class StorageSystem : EntitySystem
    {
        private readonly List<IPlayerSession> _sessionCache = new();

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<EntRemovedFromContainerMessage>(HandleEntityRemovedFromContainer);
            SubscribeLocalEvent<EntInsertedIntoContainerMessage>(HandleEntityInsertedIntoContainer);

            SubscribeLocalEvent<EntityStorageComponent, GetInteractionVerbsEvent>(AddToggleOpenVerb);
            SubscribeLocalEvent<ServerStorageComponent, GetActivationVerbsEvent>(AddOpenUiVerb);
        }

        /// <inheritdoc />
        public override void Update(float frameTime)
        {
            foreach (var component in ComponentManager.EntityQuery<ServerStorageComponent>(true))
            {
                CheckSubscribedEntities(component);
            }
        }

        private void AddToggleOpenVerb(EntityUid uid, EntityStorageComponent component, GetInteractionVerbsEvent args)
        {
            if (!args.CanAccess || !args.CanInteract)
                return;

            if (!component.CanOpen(args.User, silent: true))
                return;

            Verb verb = new("Storage:ToggleOpen");
            verb.Category = component.Open ? VerbCategory.Close : VerbCategory.Open;
            verb.Act = () => component.ToggleOpen(args.User);
            args.Verbs.Add(verb);
        }

        private void AddOpenUiVerb(EntityUid uid, ServerStorageComponent component, GetActivationVerbsEvent args)
        {
            if (!args.CanAccess || !args.CanInteract)
                return;

            if (ComponentManager.TryGetComponent(uid, out LockComponent? lockComponent) && lockComponent.Locked)
                return;

            // Get the session for the user
            var session = args.User.GetComponentOrNull<ActorComponent>()?.PlayerSession;
            if (session == null)
                return;

            // Does this player currently have the storage UI open?
            var uiOpen = component.SubscribedSessions.Contains(session);

            Verb verb = new("Storage:ToggleUI");
            verb.Act = () => component.OpenStorageUI(args.User);
            verb.Category = uiOpen ? VerbCategory.Close : VerbCategory.Open;
            args.Verbs.Add(verb);
        }

        private static void HandleEntityRemovedFromContainer(EntRemovedFromContainerMessage message)
        {
            var oldParentEntity = message.Container.Owner;

            if (oldParentEntity.TryGetComponent(out ServerStorageComponent? storageComp))
            {
                storageComp.HandleEntityMaybeRemoved(message);
            }
        }

        private static void HandleEntityInsertedIntoContainer(EntInsertedIntoContainerMessage message)
        {
            var oldParentEntity = message.Container.Owner;

            if (oldParentEntity.TryGetComponent(out ServerStorageComponent? storageComp))
            {
                storageComp.HandleEntityMaybeInserted(message);
            }
        }

        private void CheckSubscribedEntities(ServerStorageComponent storageComp)
        {

            // We have to cache the set of sessions because Unsubscribe modifies the original.
            _sessionCache.Clear();
            _sessionCache.AddRange(storageComp.SubscribedSessions);

            if (_sessionCache.Count == 0)
                return;

            var storagePos = storageComp.Owner.Transform.WorldPosition;
            var storageMap = storageComp.Owner.Transform.MapID;

            foreach (var session in _sessionCache)
            {
                var attachedEntity = session.AttachedEntity;

                // The component manages the set of sessions, so this invalid session should be removed soon.
                if (attachedEntity == null || !attachedEntity.IsValid())
                    continue;

                if (storageMap != attachedEntity.Transform.MapID)
                    continue;

                var distanceSquared = (storagePos - attachedEntity.Transform.WorldPosition).LengthSquared;
                if (distanceSquared > InteractionSystem.InteractionRangeSquared)
                {
                    storageComp.UnsubscribeSession(session);
                }
            }
        }
    }
}
