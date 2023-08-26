using Content.Server.Pulling;
using Content.Server.Security.Components;
using Content.Shared.Lock;
using Content.Shared.Pulling.Components;
using Content.Shared.Security;

namespace Content.Server.Security.Systems
{
    public sealed class DeployableBarrierSystem : EntitySystem
    {
        [Dependency] private readonly PullingSystem _pulling = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedPointLightSystem _light = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DeployableBarrierComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<DeployableBarrierComponent, LockToggledEvent>(OnLockToggled);
        }

        private void OnStartup(EntityUid uid, DeployableBarrierComponent component, ComponentStartup args)
        {
            if (!TryComp(uid, out LockComponent? lockComponent))
                return;

            ToggleBarrierDeploy(uid, lockComponent.Locked);
        }

        private void OnLockToggled(EntityUid uid, DeployableBarrierComponent component, ref LockToggledEvent args)
        {
            ToggleBarrierDeploy(uid, args.Locked);
        }

        private void ToggleBarrierDeploy(EntityUid uid, bool isDeployed)
        {
            Transform(uid).Anchored = isDeployed;

            var state = isDeployed ? DeployableBarrierState.Deployed : DeployableBarrierState.Idle;
            _appearance.SetData(uid, DeployableBarrierVisuals.State, state);

            if (TryComp<SharedPullableComponent>(uid, out var pullable))
                _pulling.TryStopPull(pullable);

            if (_light.TryGetLight(uid, out var light))
            {
                _light.SetEnabled(uid, isDeployed, light);
            }
        }
    }
}
