using Content.Shared.Electrocution;
using Content.Shared.Interaction.Events;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Content.Server.Power.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server.Ninja.Systems;

public sealed class StunProviderSystem : SharedStunProviderSystem
{
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly SharedNinjaGlovesSystem _gloves = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StunProviderComponent, InteractionAttemptEvent>(OnInteract);
    }

    /// <summary>
    /// Stun clicked mobs on the whitelist, if there is enough power.
    /// </summary>
    private void OnInteract(EntityUid uid, StunProviderComponent comp, InteractionAttemptEvent args)
    {
        if (comp.BatteryUid == null || !_gloves.AbilityCheck(uid, args, out var target))
            return;

        if (target == uid || !comp.Whitelist.IsValid(target, EntityManager))
            return;

        if (_timing.CurTime < comp.NextStun)
            return;

        // take charge from battery
        if (!_battery.TryUseCharge(comp.BatteryUid.Value, comp.StunCharge))
        {
            _popup.PopupEntity(Loc.GetString("ninja-no-power"), uid, uid);
            return;
        }

        // not holding hands with target so insuls don't matter
        _electrocution.TryDoElectrocution(target, uid, comp.StunDamage, comp.StunTime, false, ignoreInsulation: true);
        // short cooldown to prevent instant stunlocking
        comp.NextStun = _timing.CurTime + comp.Cooldown;
        Dirty(comp);
    }
}
