using Content.Shared.Administration.Logs;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Database;
using Content.Shared.Emag.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Emag.Systems;

/// How to add an emag interaction:
/// 1. Go to the system for the component you want the interaction with
/// 2. Subscribe to the GotEmaggedEvent
/// 3. Have some check for if this actually needs to be emagged or is already emagged (to stop charge waste)
/// 4. Past the check, add all the effects you desire and HANDLE THE EVENT ARGUMENT so a charge is spent
/// 5. Optionally, set Repeatable on the event to true if you don't want the emagged component to be added
public sealed partial class EmagSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmagComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, EmagComponent comp, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;

        args.Handled = TryUseEmag(uid, args.User, target, comp);
    }

    /// <summary>
    /// Tries to use the emag on a target entity
    /// </summary>
    public bool TryUseEmag(EntityUid uid, EntityUid user, EntityUid target, EmagComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return false;

        if (_tag.HasTag(target, comp.EmagImmuneTag))
            return false;

        TryComp<LimitedChargesComponent>(uid, out var charges);
        if (_charges.IsEmpty(uid, charges))
        {
            if (_net.IsClient && _timing.IsFirstTimePredicted)
                _popup.PopupEntity(Loc.GetString("emag-no-charges"), user, user);
            return false;
        }

        var handled = DoEmagEffect(user, target);
        if (!handled)
            return false;

        // only do popup on client
        if (_net.IsClient && _timing.IsFirstTimePredicted)
        {
            _popup.PopupEntity(Loc.GetString("emag-success", ("target", Identity.Entity(target, EntityManager))), user,
                user, PopupType.Medium);
        }

        _adminLogger.Add(LogType.Emag, LogImpact.High, $"{ToPrettyString(user):player} emagged {ToPrettyString(target):target}");

        if (charges != null)
            _charges.UseCharge(uid, charges);
        return true;
    }

    /// <summary>
    /// Does the emag effect on a specified entity
    /// </summary>
    public bool DoEmagEffect(EntityUid user, EntityUid target)
    {
        // prevent emagging twice
        if (HasComp<EmaggedComponent>(target))
            return false;

        var emaggedEvent = new GotEmaggedEvent(user);
        RaiseLocalEvent(target, ref emaggedEvent);

        if (emaggedEvent.Handled && !emaggedEvent.Repeatable)
            EnsureComp<EmaggedComponent>(target);
        return emaggedEvent.Handled;
    }
}

[ByRefEvent]
public record struct GotEmaggedEvent(EntityUid UserUid, bool Handled = false, bool Repeatable = false);
