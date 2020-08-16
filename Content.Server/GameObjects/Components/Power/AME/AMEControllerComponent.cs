﻿using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Server.GameObjects.Components.NodeContainer;
using Content.Server.GameObjects.Components.NodeContainer.NodeGroups;
using Content.Server.GameObjects.Components.NodeContainer.Nodes;
using Content.Server.GameObjects.Components.Power.ApcNetComponents;
using Content.Server.GameObjects.Components.Power.PowerNetComponents;
using Content.Server.Interfaces;
using Content.Server.Interfaces.GameObjects.Components.Items;
using Content.Shared.GameObjects.Components.Power.AME;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces.GameObjects.Components;
using Microsoft.EntityFrameworkCore.Internal;
using Robust.Server.GameObjects;
using Robust.Server.GameObjects.Components.Container;
using Robust.Server.GameObjects.Components.UserInterface;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components.Transform;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.ViewVariables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server.GameObjects.Components.Power.AME
{
    [RegisterComponent]
    [ComponentReference(typeof(IActivate))]
    [ComponentReference(typeof(IInteractUsing))]
    public class AMEControllerComponent : SharedAMEControllerComponent, IActivate, IInteractUsing
    {
        [Dependency] private readonly IServerNotifyManager _notifyManager = default!;
        [Dependency] private readonly ILocalizationManager _localizationManager = default!;

        [ViewVariables] private BoundUserInterface _userInterface;
        [ViewVariables] private bool _injecting;
        [ViewVariables] private int _injectionAmount;

        private AppearanceComponent _appearance;
        private PowerReceiverComponent _powerReceiver;
        private PowerSupplierComponent _powerSupplier;

        private bool Powered => _powerReceiver.Powered;

        [ViewVariables]
        private int _stability = 100;

        private ContainerSlot _jarSlot;
        [ViewVariables] private bool HasJar => _jarSlot.ContainedEntity != null;

        public override void Initialize()
        {
            base.Initialize();

            _userInterface = Owner.GetComponent<ServerUserInterfaceComponent>()
                .GetBoundUserInterface(AMEControllerUiKey.Key);
            _userInterface.OnReceiveMessage += OnUiReceiveMessage;

            _appearance = Owner.GetComponent<AppearanceComponent>();

            _powerReceiver = Owner.GetComponent<PowerReceiverComponent>();
            _powerReceiver.OnPowerStateChanged += OnPowerChanged;

            _powerSupplier = Owner.GetComponent<PowerSupplierComponent>();

            _injecting = false;
            _injectionAmount = 2;
            _jarSlot = ContainerManagerComponent.Ensure<ContainerSlot>($"{Name}-fuelJarContainer", Owner);
        }

        internal void OnUpdate(float frameTime)
        {
            if(!_injecting)
            {
                return;
            }

            _jarSlot.ContainedEntity.TryGetComponent<AMEFuelContainerComponent>(out var fuelJar);
            if(fuelJar != null)
            {
                _powerSupplier.SupplyRate = GetAMENodeGroup().InjectFuel(_injectionAmount);
                fuelJar.FuelAmount -= _injectionAmount;
                UpdateUserInterface();
            }

            _stability = GetAMENodeGroup().GetTotalStability();

            UpdateDisplay(_stability);

            if(_stability <= 0) { GetAMENodeGroup().ExplodeCores(); }

        }

        /// <summary>
        /// Called when you click the owner entity with an empty hand. Opens the UI client-side if possible.
        /// </summary>
        /// <param name="args">Data relevant to the event such as the actor which triggered it.</param>
        void IActivate.Activate(ActivateEventArgs args)
        {
            if (!args.User.TryGetComponent(out IActorComponent actor))
            {
                return;
            }

            if (!args.User.TryGetComponent(out IHandsComponent hands))
            {
                _notifyManager.PopupMessage(Owner.Transform.GridPosition, args.User,
                    _localizationManager.GetString("You have no hands."));
                return;
            }

            var activeHandEntity = hands.GetActiveHand?.Owner;
            if (activeHandEntity == null)
            {
                _userInterface.Open(actor.playerSession);
            }
        }

        private void OnPowerChanged(object sender, PowerStateEventArgs e)
        {
            UpdateUserInterface();
        }

        private AMEControllerBoundUserInterfaceState GetUserInterfaceState()
        {
            var jar = _jarSlot.ContainedEntity;
            if (jar == null)
            {
                return new AMEControllerBoundUserInterfaceState(Powered, IsMasterController(), false, HasJar, 0, _injectionAmount, GetCoreCount());
            }

            var jarcomponent = jar.GetComponent<AMEFuelContainerComponent>();
            return new AMEControllerBoundUserInterfaceState(Powered, IsMasterController(), _injecting, HasJar, jarcomponent.FuelAmount, _injectionAmount, GetCoreCount());
        }

        /// <summary>
        /// Checks whether the player entity is able to use the controller.
        /// </summary>
        /// <param name="playerEntity">The player entity.</param>
        /// <returns>Returns true if the entity can use the controller, and false if it cannot.</returns>
        private bool PlayerCanUseController(IEntity playerEntity, bool needsPower = true)
        {
            //Need player entity to check if they are still able to use the dispenser
            if (playerEntity == null)
                return false;
            //Check if player can interact in their current state
            if (!ActionBlockerSystem.CanInteract(playerEntity) || !ActionBlockerSystem.CanUse(playerEntity))
                return false;
            //Check if device is powered
            if (needsPower && !Powered)
                return false;

            return true;
        }

        private void UpdateUserInterface()
        {
            var state = GetUserInterfaceState();
            _userInterface.SetState(state);
        }

        /// <summary>
        /// Handles ui messages from the client. For things such as button presses
        /// which interact with the world and require server action.
        /// </summary>
        /// <param name="obj">A user interface message from the client.</param>
        private void OnUiReceiveMessage(ServerBoundUserInterfaceMessage obj)
        {
            var msg = (UiButtonPressedMessage) obj.Message;
            var needsPower = msg.Button switch
            {
                UiButton.Eject => false,
                _ => true,
            };

            if (!PlayerCanUseController(obj.Session.AttachedEntity, needsPower))
                return;

            switch (msg.Button)
            {
                case UiButton.Eject:
                    TryEject(obj.Session.AttachedEntity);
                    break;
                case UiButton.ToggleInjection:
                    ToggleInjection();
                    break;
                case UiButton.IncreaseFuel:
                    _injectionAmount += 2;
                    break;
                case UiButton.DecreaseFuel:
                    _injectionAmount = _injectionAmount > 0 ? _injectionAmount -= 2 : 0;
                    break;
                case UiButton.RefreshParts:
                    RefreshParts();
                    break;
            }

                GetAMENodeGroup().UpdateCoreVisuals(_injectionAmount, _injecting);

            UpdateUserInterface();
            ClickSound();
        }

        private void TryEject(IEntity user)
        {
            if (!HasJar || _injecting)
                return;

            var jar = _jarSlot.ContainedEntity;
            _jarSlot.Remove(_jarSlot.ContainedEntity);
            UpdateUserInterface();

            if (!user.TryGetComponent<HandsComponent>(out var hands) || !jar.TryGetComponent<ItemComponent>(out var item))
                return;
            if (hands.CanPutInHand(item))
                hands.PutInHand(item);
        }

        private void ToggleInjection()
        {
            if (!_injecting)
            {
                _appearance.SetData(AMEControllerVisuals.DisplayState, "on");
            }
            else
            {
                _appearance.SetData(AMEControllerVisuals.DisplayState, "off");
                _powerSupplier.SupplyRate = 0;
            }
            _injecting = !_injecting;
            UpdateUserInterface();
        }


        private void UpdateDisplay(int stability)
        {
            _appearance.TryGetData<string>(AMEControllerVisuals.DisplayState, out var state);

            var newState = "on";

            if(stability < 50) { newState = "critical"; }
            if(stability < 10) { newState = "fuck"; }

            if (state != newState)
            {
                _appearance.SetData(AMEControllerVisuals.DisplayState, newState);
            }

        }

        private void RefreshParts()
        {
            GetAMENodeGroup().RefreshAMENodes(this);
            UpdateUserInterface();
        }

        private AMENodeGroup GetAMENodeGroup()
        {
            Owner.TryGetComponent(out NodeContainerComponent nodeContainer);

            var engineNodeGroup = nodeContainer.Nodes
            .Select(node => node.NodeGroup)
            .OfType<AMENodeGroup>()
            .First();

            return engineNodeGroup;
        }

        private bool IsMasterController()
        {
            if(GetAMENodeGroup().MasterController == this)
            {
                return true;
            }

            return false;
        }

        private int GetCoreCount()
        {
            var coreCount = 0;

            if(GetAMENodeGroup() != null)
            {
                coreCount = GetAMENodeGroup().CoreCount;
            }

            return coreCount;
        }


        private void ClickSound()
        {

            EntitySystem.Get<AudioSystem>().PlayFromEntity("/Audio/Machines/machine_switch.ogg", Owner, AudioParams.Default.WithVolume(-2f));

        }

        async Task<bool> IInteractUsing.InteractUsing(InteractUsingEventArgs args)
        {
            if (!args.User.TryGetComponent(out IHandsComponent hands))
            {
                _notifyManager.PopupMessage(Owner.Transform.GridPosition, args.User,
                    _localizationManager.GetString("You have no hands."));
                return true;
            }

            var activeHandEntity = hands.GetActiveHand.Owner;
            if (activeHandEntity.TryGetComponent<AMEFuelContainerComponent>(out var fuelContainer))
            {
                if (HasJar)
                {
                    _notifyManager.PopupMessage(Owner.Transform.GridPosition, args.User,
                        _localizationManager.GetString("The controller already has a jar loaded."));
                }

                else
                {
                    _jarSlot.Insert(activeHandEntity);
                    _notifyManager.PopupMessage(Owner.Transform.GridPosition, args.User,
                        _localizationManager.GetString("You insert the jar into the fuel slot."));
                    UpdateUserInterface();
                }
            }
            else
            {
                _notifyManager.PopupMessage(Owner.Transform.GridPosition, args.User,
                    _localizationManager.GetString("You can't put that in the controller..."));
            }

            return true;
        }
    }

}
