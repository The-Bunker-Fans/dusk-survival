using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.Components.SolutionManager;
using Content.Server.Chemistry.Events;
using Content.Server.Nutrition.EntitySystems;
using Content.Shared.Inventory;
using JetBrains.Annotations;
using Robust.Shared.Physics.Dynamics;

namespace Content.Server.Chemistry.EntitySystems
{
    [UsedImplicitly]
    internal sealed class SolutionInjectOnCollideSystem : EntitySystem
    {
        [Dependency] private readonly SolutionContainerSystem _solutionsSystem = default!;
        [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SolutionInjectOnCollideComponent, ComponentInit>(HandleInit);
            SubscribeLocalEvent<SolutionInjectOnCollideComponent, StartCollideEvent>(HandleInjection);
            SubscribeLocalEvent<SolutionInjectOnCollideComponent, TransferThroughFaceAttemptEvent>(OnTransferThroughFaceAttemptEvent);
        }

        private void OnTransferThroughFaceAttemptEvent(EntityUid uid, SolutionInjectOnCollideComponent component, TransferThroughFaceAttemptEvent args)
        {
            IngestionBlockerComponent blocker;

            if (_inventorySystem.TryGetSlotEntity(uid, "head", out var headUid) &&
                EntityManager.TryGetComponent(headUid, out blocker) &&
                blocker.Enabled)
            {
                args.Cancel();
            }
        }

        private void HandleInit(EntityUid uid, SolutionInjectOnCollideComponent component, ComponentInit args)
        {
            component.Owner
                .EnsureComponentWarn<SolutionContainerManagerComponent>($"{nameof(SolutionInjectOnCollideComponent)} requires a SolutionContainerManager on {component.Owner}!");
        }

        private void HandleInjection(EntityUid uid, SolutionInjectOnCollideComponent component, StartCollideEvent args)
        {
            if (!EntityManager.TryGetComponent<BloodstreamComponent?>(args.OtherFixture.Body.Owner, out var bloodstream) ||
                !_solutionsSystem.TryGetInjectableSolution(component.Owner, out var solution)) return;

            if(component.CanPenetrateHelmet == false
               && IsFaceBlocked(args.OtherFixture.Body.Owner)
               && component.CanPenetrateArmor == false) //TODO : Implelement the armor check in another PR
                return;

            var solRemoved = solution.SplitSolution(component.TransferAmount);
            var solRemovedVol = solRemoved.TotalVolume;

            var solToInject = solRemoved.SplitSolution(solRemovedVol * component.TransferEfficiency);

            _bloodstreamSystem.TryAddToChemicals((args.OtherFixture.Body).Owner, solToInject, bloodstream);
        }

        public bool IsFaceBlocked(EntityUid uid)
        {
            var attemptEvent = new TransferThroughFaceAttemptEvent(uid);
            RaiseLocalEvent( uid , attemptEvent , false);
            if (attemptEvent.Cancelled) return true ;
            return false;
        }
    }
}
