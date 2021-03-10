#nullable enable
using Content.Client.Animations;
using Content.Client.UserInterface;
using Content.Shared.GameObjects.Components.Items;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Players;
using Robust.Shared.ViewVariables;
using System.Collections.Generic;
using System.Linq;

namespace Content.Client.GameObjects.Components.Items
{
    [RegisterComponent]
    [ComponentReference(typeof(ISharedHandsComponent))]
    [ComponentReference(typeof(SharedHandsComponent))]
    public class HandsComponent : SharedHandsComponent
    {
        [Dependency] private readonly IGameHud _gameHud = default!;

        [ViewVariables]
        private HandsGui Gui { get; set; } = default!;

        [ViewVariables(VVAccess.ReadWrite)]
        private int ActiveHand { get; set; } //TODO: should this be nullable?

        [ViewVariables]
        public IReadOnlyList<ClientHand> Hands => _hands;
        private readonly List<ClientHand> _hands = new();

        [ViewVariables]
        public IEntity? ActiveItem => Hands.ElementAtOrDefault(ActiveHand)?.HeldItem;

        [ComponentDependency]
        private ISpriteComponent? _sprite = default!;

        [ViewVariables]
        private string? ActiveHandName => Hands.ElementAtOrDefault(ActiveHand)?.Name; //debug var

        public override void OnAdd()
        {
            base.OnAdd();
            Gui = new HandsGui(this); //TODO: subscripe msg sends to ui events
        }

        public override void Initialize()
        {
            base.Initialize();

            _gameHud.HandsContainer.AddChild(Gui);
            Owner.TryGetComponent(out _sprite); //TODO: use component dependency?
        }

        public override void OnRemove()
        {
            Gui.Dispose();
            base.OnRemove();
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            if (curState is not HandsComponentState state)
                return;

            ActiveHand = state.ActiveIndex;

            foreach (var hand in _hands)
            {
                if (_sprite == null)
                    continue;

                var layerKey = GetHandLayerKey(hand.Name);
                var layer = _sprite.LayerMapGet(layerKey);
                _sprite.RemoveLayer(layer);
                _sprite.LayerMapRemove(layerKey);
            }
            _hands.Clear();

            foreach (var handState in state.Hands)
            {
                var newHand = new ClientHand(handState, GetHeldItem(handState.EntityUid));
                _hands.Add(newHand);
            }
            OnHandsModified();

            IEntity? GetHeldItem(EntityUid? uid)
            {
                IEntity? heldItem = null;
                if (uid != null)
                    Owner.EntityManager.TryGetEntity(uid.Value, out heldItem);

                return heldItem;
            }
        }

        public override void HandleMessage(ComponentMessage message, IComponent? component)
        {
            base.HandleMessage(message, component);

            switch (message)
            {
                case PlayerAttachedMsg:
                    HandlePlayerAttachedMsg();
                    break;
                case PlayerDetachedMsg:
                    HandlePlayerDetachedMsg();
                    break;
                case HandEnabledMsg msg:
                    HandleHandEnabledMsg(msg);
                    break;
                case HandDisabledMsg msg:
                    HandleHandDisabledMsg(msg);
                    break;
            }
        }

        public override void HandleNetworkMessage(ComponentMessage message, INetChannel netChannel, ICommonSession? session = null)
        {
            base.HandleNetworkMessage(message, netChannel, session);

            switch (message)
            {
                case AnimatePickupEntityMessage msg:
                    HandleAnimatePickupEntityMessage(msg);
                    break;
            }
        }

        public void RefreshInHands()
        {
            SetGuiState(); //might be a more straightforward way to handle updating just th eitem names
        }

        public override bool IsHolding(IEntity entity)
        {
            foreach (var hand in Hands)
            {
                if (hand.HeldItem == entity)
                    return true;
            }
            return false;
        }

        private void HandleAnimatePickupEntityMessage(AnimatePickupEntityMessage msg)
        {
            if (!Owner.EntityManager.TryGetEntity(msg.EntityId, out var entity))
                return;

            ReusableAnimations.AnimateEntityPickup(entity, msg.EntityPosition, Owner.Transform.WorldPosition);
        }

        private void HandlePlayerAttachedMsg()
        {
        }

        private void HandlePlayerDetachedMsg()
        {
        }

        private void HandleHandEnabledMsg(HandEnabledMsg msg)
        {
        }

        private void HandleHandDisabledMsg(HandDisabledMsg msg)
        {
        }

        public void SendChangeHand(string index)
        {
        }

        private void OnHandsModified()
        {
            MakeHandTextures();
            SetGuiState();
        }

        private void MakeHandTextures()
        {
            if (_sprite == null)
                return;

            foreach (var hand in Hands)
            {
                var key = GetHandLayerKey(hand.Name);
                _sprite.LayerMapReserveBlank(key);

                var heldItem = hand.HeldItem;
                if (heldItem == null || !heldItem.TryGetComponent(out ItemComponent? item))
                    continue;

                var maybeInHands = item.GetInHandStateInfo(hand.Location);
                if (maybeInHands == null)
                    continue;

                var (rsi, state, color) = maybeInHands.Value;

                if (rsi == null)
                {
                    _sprite.LayerSetVisible(key, false);
                }
                else
                {
                    _sprite.LayerSetColor(key, color);
                    _sprite.LayerSetVisible(key, true);
                    _sprite.LayerSetState(key, state, rsi);
                }
            }
        }

        private void SetGuiState()
        {
            Gui.SetState(GetHandsGuiState());
        }

        private HandsGuiState GetHandsGuiState()
        {
            var handStates = new List<GuiHand>();

            foreach (var hand in _hands)
            {
                var handState = new GuiHand(hand.Name, hand.Location, hand.HeldItem);
                handStates.Add(handState);
            }
            return new HandsGuiState(handStates, ActiveHand);
        }

        private object GetHandLayerKey(string handName)
        {
            return $"hand-{handName}";
        }
    }

    public class ClientHand
    {
        public string Name { get; }
        public HandLocation Location { get; }
        public IEntity? HeldItem { get; }

        public ClientHand(SharedHand hand, IEntity? heldItem)
        {
            Name = hand.Name;
            Location = hand.Location;
            HeldItem = heldItem;
        }
    }
}
