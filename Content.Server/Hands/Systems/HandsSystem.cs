using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Hands.Components;
using Content.Server.Inventory.Components;
using Content.Server.Items;
using Content.Server.Stack;
using Content.Server.Storage.Components;
using Content.Server.Strip;
using Content.Server.Throwing;
using Content.Shared.ActionBlocker;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Input;
using Content.Shared.Physics.Pull;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Input.Binding;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Players;
using Robust.Shared.Utility;
using static Content.Shared.Inventory.EquipmentSlotDefines;

namespace Content.Server.Hands.Systems
{
    [UsedImplicitly]
    internal sealed class HandsSystem : SharedHandsSystem
    {
        [Dependency] private readonly StackSystem _stackSystem = default!;
        [Dependency] private readonly HandVirtualItemSystem _virtualItemSystem = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
        [Dependency] private readonly AdminLogSystem _logSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<HandsComponent, ExaminedEvent>(HandleExamined);
            SubscribeNetworkEvent<ActivateInHandMsg>(HandleActivateInHand);
            SubscribeNetworkEvent<ClientInteractUsingInHandMsg>(HandleInteractUsingInHand);
            SubscribeNetworkEvent<UseInHandMsg>(HandleUseInHand);
            SubscribeNetworkEvent<MoveItemFromHandMsg>(HandleMoveItemFromHand);

            SubscribeLocalEvent<HandsComponent, PullAttemptMessage>(HandlePullAttempt);
            SubscribeLocalEvent<HandsComponent, PullStartedMessage>(HandlePullStarted);
            SubscribeLocalEvent<HandsComponent, PullStoppedMessage>(HandlePullStopped);

            SubscribeLocalEvent<HandsComponent, ComponentGetState>(GetComponentState);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.ActivateItemInHand, InputCmdHandler.FromDelegate(HandleActivateItem))
                .Bind(ContentKeyFunctions.AltActivateItemInHand, InputCmdHandler.FromDelegate(HandleAltActivateItem))
                .Bind(ContentKeyFunctions.ThrowItemInHand, new PointerInputCmdHandler(HandleThrowItem))
                .Bind(ContentKeyFunctions.SmartEquipBackpack, InputCmdHandler.FromDelegate(HandleSmartEquipBackpack))
                .Bind(ContentKeyFunctions.SmartEquipBelt, InputCmdHandler.FromDelegate(HandleSmartEquipBelt))
                .Register<HandsSystem>();
        }

        public override void Shutdown()
        {
            base.Shutdown();

            CommandBinds.Unregister<HandsSystem>();
        }

        private void GetComponentState(EntityUid uid, HandsComponent hands, ref ComponentGetState args)
        {
            args.State = new HandsComponentState(hands.Hands, hands.ActiveHand);
        }

        #region EntityInsertRemove
        public override void RemoveHeldEntityFromHand(EntityUid uid, Hand hand, SharedHandsComponent? hands = null)
        {
            base.RemoveHeldEntityFromHand(uid, hand, hands);

            if (EntityManager.TryGetComponent(uid, out StrippableComponent strip))
                strip.UpdateSubscribed();

            var entity = hand.HeldEntity;

            if (entity == null)
                return;

            if (EntityManager.TryGetComponent(entity.Uid, out SpriteComponent? sprite))
                sprite.RenderOrder = EntityManager.CurrentTick.Value;
        }

        public override void PutEntityIntoHand(EntityUid uid, Hand hand, IEntity entity, SharedHandsComponent? hands = null)
        {
            base.PutEntityIntoHand(uid, hand ,entity, hands);

            if (EntityManager.TryGetComponent(uid, out StrippableComponent strip))
                strip.UpdateSubscribed();
            
            _logSystem.Add(LogType.Pickup, LogImpact.Low, $"{EntityManager.GetEntity(uid)} picked up {entity}");
        }

        public override void PickupAnimation(IEntity item, EntityCoordinates initialPosition, Vector2 finalPosition,
            EntityUid? exclude)
        {
            var filter = Filter.Pvs(item.Uid);

            if (exclude != null)
                filter = filter.RemoveWhereAttachedEntity(entity => entity == exclude);

            RaiseNetworkEvent(new PickupAnimationEvent(item.Uid, initialPosition, finalPosition), filter);
        }
        #endregion

        #region pulling
        private static void HandlePullAttempt(EntityUid uid, HandsComponent component, PullAttemptMessage args)
        {
            // Cancel pull if all hands full.
            if (component.Hands.All(hand => !hand.IsEmpty))
                args.Cancelled = true;
        }


        private void HandlePullStarted(EntityUid uid, HandsComponent component, PullStartedMessage args)
        {
            if (!_virtualItemSystem.TrySpawnVirtualItemInHand(args.Pulled.Owner.Uid, uid))
            {
                DebugTools.Assert("Unable to find available hand when starting pulling??");
            }
        }

        private void HandlePullStopped(EntityUid uid, HandsComponent component, PullStoppedMessage args)
        {
            // Try find hand that is doing this pull.
            // and clear it.
            foreach (var hand in component.Hands)
            {
                if (hand.HeldEntity == null
                    || !hand.HeldEntity.TryGetComponent(out HandVirtualItemComponent? virtualItem)
                    || virtualItem.BlockingEntity != args.Pulled.Owner.Uid)
                    continue;

                hand.HeldEntity.Delete();
                break;
            }
        }
        #endregion

        #region interactions
        private void HandleMoveItemFromHand(MoveItemFromHandMsg msg, EntitySessionEventArgs args)
        {
            if (!TryGetHandsComp(args.SenderSession, out var hands))
                return;

            hands.TryMoveHeldEntityToActiveHand(msg.HandName);
        }

        private void HandleUseInHand(UseInHandMsg msg, EntitySessionEventArgs args)
        {
            if (!TryGetHandsComp(args.SenderSession, out var hands))
                return;

            hands.ActivateItem();
        }

        private void HandleInteractUsingInHand(ClientInteractUsingInHandMsg msg, EntitySessionEventArgs args)
        {
            if (!TryGetHandsComp(args.SenderSession, out var hands))
                return;

            hands.InteractHandWithActiveHand(msg.HandName);
        }
        private void HandleActivateInHand(ActivateInHandMsg msg, EntitySessionEventArgs args)
        {
            if (!TryGetHandsComp(args.SenderSession, out var hands))
                return;

            hands.ActivateHeldEntity(msg.HandName);
        }

        private void HandleActivateItem(ICommonSession? session)
        {
            if (!TryGetHandsComp(session, out var hands))
                return;

            hands.ActivateItem();
        }

        private void HandleAltActivateItem(ICommonSession? session)
        {
            if (!TryGetHandsComp(session, out var hands))
                return;

            hands.ActivateItem(altInteract: true);
        }

        private bool HandleThrowItem(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
        {
            if (session is not IPlayerSession playerSession)
                return false;

            var playerEnt = playerSession.AttachedEntity;

            if (playerEnt == null ||
                !playerEnt.IsValid() ||
                playerEnt.IsInContainer() ||
                !playerEnt.TryGetComponent(out SharedHandsComponent? hands) ||
                !hands.TryGetActiveHeldEntity(out var throwEnt) ||
                !_actionBlockerSystem.CanThrow(playerEnt.Uid))
                return false;

            if (throwEnt.TryGetComponent(out StackComponent? stack) && stack.Count > 1 && stack.ThrowIndividually)
            {
                var splitStack = _stackSystem.Split(throwEnt.Uid, 1, playerEnt.Transform.Coordinates, stack);

                if (splitStack == null)
                    return false;

                throwEnt = EntityManager.GetEntity(splitStack.Value);
            }
            else if (!hands.Drop(throwEnt))
                return false;

            var direction = coords.ToMapPos(EntityManager) - playerEnt.Transform.WorldPosition;
            if (direction == Vector2.Zero)
                return true;

            direction = direction.Normalized * Math.Min(direction.Length, hands.ThrowRange);

            var throwStrength = hands.ThrowForceMultiplier;
            throwEnt.TryThrow(direction, throwStrength, playerEnt);

            return true;
        }

        private void HandleSmartEquipBackpack(ICommonSession? session)
        {
            HandleSmartEquip(session, Slots.BACKPACK);
        }

        private void HandleSmartEquipBelt(ICommonSession? session)
        {
            HandleSmartEquip(session, Slots.BELT);
        }

        private void HandleSmartEquip(ICommonSession? session, Slots equipmentSlot)
        {
            if (session is not IPlayerSession playerSession)
                return;

            var plyEnt = playerSession.AttachedEntity;

            if (plyEnt == null || !plyEnt.IsValid())
                return;

            if (!plyEnt.TryGetComponent(out SharedHandsComponent? hands) ||
                !plyEnt.TryGetComponent(out InventoryComponent? inventory))
                return;

            if (!inventory.TryGetSlotItem(equipmentSlot, out ItemComponent? equipmentItem) ||
                !equipmentItem.Owner.TryGetComponent(out ServerStorageComponent? storageComponent))
            {
                plyEnt.PopupMessage(Loc.GetString("hands-system-missing-equipment-slot", ("slotName", SlotNames[equipmentSlot].ToLower())));
                return;
            }

            if (hands.ActiveHandIsHoldingEntity())
            {
                storageComponent.PlayerInsertHeldEntity(plyEnt);
            }
            else if (storageComponent.StoredEntities != null)
            {
                if (storageComponent.StoredEntities.Count == 0)
                {
                    plyEnt.PopupMessage(Loc.GetString("hands-system-empty-equipment-slot", ("slotName", SlotNames[equipmentSlot].ToLower())));
                }
                else
                {
                    var lastStoredEntity = Enumerable.Last(storageComponent.StoredEntities);
                    if (storageComponent.Remove(lastStoredEntity))
                    {
                        if (!hands.TryPickupEntityToActiveHand(lastStoredEntity, animateUser: true))
                            lastStoredEntity.Transform.Coordinates = plyEnt.Transform.Coordinates;
                    }
                }
            }
        }
        #endregion

        //TODO: Actually shows all items/clothing/etc.
        private void HandleExamined(EntityUid uid, HandsComponent component, ExaminedEvent args)
        {
            foreach (var inhand in component.GetAllHeldItems())
            {
                if (inhand.Owner.HasComponent<HandVirtualItemComponent>())
                    continue;

                args.PushText(Loc.GetString("comp-hands-examine", ("user", component.Owner), ("item", inhand.Owner)));
            }
        }

        private static bool TryGetHandsComp(
            ICommonSession? session,
            [NotNullWhen(true)] out SharedHandsComponent? hands)
        {
            hands = default;

            if (session is not IPlayerSession playerSession)
                return false;

            var playerEnt = playerSession.AttachedEntity;

            if (playerEnt == null || !playerEnt.IsValid())
                return false;

            return playerEnt.TryGetComponent(out hands);
        }
    }
}
