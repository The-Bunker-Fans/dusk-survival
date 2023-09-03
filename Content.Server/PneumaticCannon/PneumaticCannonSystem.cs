using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Storage.EntitySystems;
using Content.Server.Stunnable;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.PneumaticCannon;
using Content.Shared.StatusEffect;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Server.PneumaticCannon;

public sealed partial class PneumaticCannonSystem : SharedPneumaticCannonSystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private GasTankSystem _gasTank = default!;
    [Dependency] private StunSystem _stun = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PneumaticCannonComponent, InteractUsingEvent>(OnInteractUsing, before: new []{ typeof(StorageSystem) });
        SubscribeLocalEvent<PneumaticCannonComponent, GunShotEvent>(OnShoot);
        SubscribeLocalEvent<PneumaticCannonComponent, ContainerIsInsertingAttemptEvent>(OnContainerInserting);
    }

    private void OnInteractUsing(EntityUid uid, PneumaticCannonComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ToolComponent>(args.Used, out var tool))
            return;

        if (!tool.Qualities.Contains(component.ToolModifyPower))
            return;

        var val = (int) component.Power;
        val = (val + 1) % (int) PneumaticCannonPower.Len;
        component.Power = (PneumaticCannonPower) val;

        Popup.PopupEntity(Loc.GetString("pneumatic-cannon-component-change-power",
            ("power", component.Power.ToString())), uid, args.User);

        if (TryComp<GunComponent>(uid, out var gun))
        {
            gun.ProjectileSpeed = GetProjectileSpeedFromPower(component);
        }

        args.Handled = true;
    }

    private void OnContainerInserting(EntityUid uid, PneumaticCannonComponent component, ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != PneumaticCannonComponent.TankSlotId)
            return;

        if (!TryComp<GasTankComponent>(args.EntityUid, out var gas))
            return;

        // only accept tanks if it uses gas
        if (gas.Air.TotalMoles >= component.GasUsage && component.GasUsage > 0f)
            return;

        args.Cancel();
    }

    private void OnShoot(EntityUid uid, PneumaticCannonComponent component, ref GunShotEvent args)
    {
        // require a gas tank if it uses gas
        var gas = GetGas(uid);
        if (gas == null && component.GasUsage > 0f)
            return;

        if(TryComp<StatusEffectsComponent>(args.User, out var status)
           && component.Power == PneumaticCannonPower.High)
        {
            _stun.TryParalyze(args.User, TimeSpan.FromSeconds(component.HighPowerStunTime), true, status);
            Popup.PopupEntity(Loc.GetString("pneumatic-cannon-component-power-stun",
                ("cannon", component.Owner)), uid, args.User);
        }

        // ignore gas stuff if the cannon doesn't use any
        if (gas == null)
            return;

        // this should always be possible, as we'll eject the gas tank when it no longer is
        var environment = _atmos.GetContainingMixture(component.Owner, false, true);
        var removed = _gasTank.RemoveAir(gas, component.GasUsage);
        if (environment != null && removed != null)
        {
            _atmos.Merge(environment, removed);
        }

        if (gas.Air.TotalMoles >= component.GasUsage)
            return;

        // eject gas tank
        _slots.TryEject(uid, PneumaticCannonComponent.TankSlotId, args.User, out _);
    }

    /// <summary>
    ///     Returns whether the pneumatic cannon has enough gas to shoot an item, as well as the tank itself.
    /// </summary>
    private GasTankComponent? GetGas(EntityUid uid)
    {
        if (!Container.TryGetContainer(uid, PneumaticCannonComponent.TankSlotId, out var container) ||
            container is not ContainerSlot slot || slot.ContainedEntity is not {} contained)
            return null;

        return TryComp<GasTankComponent>(contained, out var gasTank) ? gasTank : null;
    }

    private float GetProjectileSpeedFromPower(PneumaticCannonComponent component)
    {
        return component.Power switch
        {
            PneumaticCannonPower.High => component.BaseProjectileSpeed * 4f,
            PneumaticCannonPower.Medium => component.BaseProjectileSpeed,
            PneumaticCannonPower.Low or _ => component.BaseProjectileSpeed * 0.5f,
        };
    }
}
