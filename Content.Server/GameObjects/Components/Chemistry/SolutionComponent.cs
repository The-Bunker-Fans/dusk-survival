﻿using System;
using Content.Server.GameObjects.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.GameObjects;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.Components.Chemistry
{
    /// <summary>
    ///     Shared ECS component that manages a liquid solution of reagents.
    /// </summary>
    [RegisterComponent]
    internal class SolutionComponent : Shared.GameObjects.Components.Chemistry.SolutionComponent, IExamine, IDestroyAct
    {
#pragma warning disable 649
        [Dependency] private readonly IPrototypeManager _prototypeManager;
        [Dependency] private readonly ILocalizationManager _localizationManager;
#pragma warning restore 649

        /// <summary>
        /// Reference to the dispenser containing this solution, if applicable.
        /// <para>Used to provide easy UI update for that dispenser when a reaction occurs.</para>
        /// </summary>
        [CanBeNull] public ReagentDispenserComponent Dispenser = null;

        /// <summary>
        ///     Transfers solution from the held container to the target container.
        /// </summary>
        [Verb]
        private sealed class FillTargetVerb : Verb<SolutionComponent>
        { 
            protected override string GetText(IEntity user, SolutionComponent component)
            {
                if(!user.TryGetComponent<HandsComponent>(out var hands))
                    return "<I SHOULD BE INVISIBLE>";

                if(hands.GetActiveHand == null)
                    return "<I SHOULD BE INVISIBLE>";

                var heldEntityName = hands.GetActiveHand.Owner?.Prototype?.Name ?? "<Item>";
                var myName = component.Owner.Prototype?.Name ?? "<Item>";

                return $"Transfer liquid from [{heldEntityName}] to [{myName}].";
            }

            protected override VerbVisibility GetVisibility(IEntity user, SolutionComponent component)
            {
                if (user.TryGetComponent<HandsComponent>(out var hands))
                {
                    if (hands.GetActiveHand != null)
                    {
                        if (hands.GetActiveHand.Owner.TryGetComponent<SolutionComponent>(out var solution))
                        {
                            if ((solution.Capabilities & SolutionCaps.PourOut) != 0 && (component.Capabilities & SolutionCaps.PourIn) != 0)
                                return VerbVisibility.Visible;
                        }
                    }
                }

                return VerbVisibility.Invisible;
            }

            protected override void Activate(IEntity user, SolutionComponent component)
            {
                if (!user.TryGetComponent<HandsComponent>(out var hands))
                    return;

                if (hands.GetActiveHand == null)
                    return;

                if (!hands.GetActiveHand.Owner.TryGetComponent<SolutionComponent>(out var handSolutionComp))
                    return;

                if ((handSolutionComp.Capabilities & SolutionCaps.PourOut) == 0 || (component.Capabilities & SolutionCaps.PourIn) == 0)
                    return;

                var transferQuantity = Math.Min(component.MaxVolume - component.CurrentVolume, handSolutionComp.CurrentVolume);
                transferQuantity = Math.Min(transferQuantity, 10);

                // nothing to transfer
                if (transferQuantity <= 0)
                    return;

                var transferSolution = handSolutionComp.SplitSolution(transferQuantity);
                component.TryAddSolution(transferSolution);

            }
        }

        void IExamine.Examine(FormattedMessage message)
        {
            message.AddText(_localizationManager.GetString("Contains:\n"));
            foreach (var reagent in ReagentList)
            {
                if (_prototypeManager.TryIndex(reagent.ReagentId, out ReagentPrototype proto))
                {
                    message.AddText($"{proto.Name}: {reagent.Quantity}u\n");
                }
                else
                {
                    message.AddText(_localizationManager.GetString("Unknown reagent:") + $"{reagent.Quantity}u\n");
                }
            }
        }

        /// <summary>
        ///     Transfers solution from a target container to the held container.
        /// </summary>
        [Verb]
        private sealed class EmptyTargetVerb : Verb<SolutionComponent>
        {
            protected override string GetText(IEntity user, SolutionComponent component)
            {
                if (!user.TryGetComponent<HandsComponent>(out var hands))
                    return "<I SHOULD BE INVISIBLE>";

                if (hands.GetActiveHand == null)
                    return "<I SHOULD BE INVISIBLE>";

                var heldEntityName = hands.GetActiveHand.Owner?.Prototype?.Name ?? "<Item>";
                var myName = component.Owner.Prototype?.Name ?? "<Item>";

                return $"Transfer liquid from [{myName}] to [{heldEntityName}].";
            }

            protected override VerbVisibility GetVisibility(IEntity user, SolutionComponent component)
            {
                if (user.TryGetComponent<HandsComponent>(out var hands))
                {
                    if (hands.GetActiveHand != null)
                    {
                        if (hands.GetActiveHand.Owner.TryGetComponent<SolutionComponent>(out var solution))
                        {
                            if ((solution.Capabilities & SolutionCaps.PourIn) != 0 && (component.Capabilities & SolutionCaps.PourOut) != 0)
                                return VerbVisibility.Visible;
                        }
                    }
                }

                return VerbVisibility.Invisible;
            }

            protected override void Activate(IEntity user, SolutionComponent component)
            {
                if (!user.TryGetComponent<HandsComponent>(out var hands))
                    return;

                if (hands.GetActiveHand == null)
                    return;

                if(!hands.GetActiveHand.Owner.TryGetComponent<SolutionComponent>(out var handSolutionComp))
                    return;

                if ((handSolutionComp.Capabilities & SolutionCaps.PourIn) == 0 || (component.Capabilities & SolutionCaps.PourOut) == 0)
                    return;

                var transferQuantity = Math.Min(handSolutionComp.MaxVolume - handSolutionComp.CurrentVolume, component.CurrentVolume);
                transferQuantity = Math.Min(transferQuantity, 10);

                // pulling from an empty container, pointless to continue
                if (transferQuantity <= 0)
                    return;

                var transferSolution = component.SplitSolution(transferQuantity);
                handSolutionComp.TryAddSolution(transferSolution);
            }
        }

        public void OnDestroy(DestructionEventArgs eventArgs)
        {
            Dispenser?.UpdateUserInterface();
        }
    }
}
