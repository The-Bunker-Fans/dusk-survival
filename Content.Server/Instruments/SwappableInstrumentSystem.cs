using Content.Shared.Instruments;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Server.Instruments;

public sealed class SwappableInstrumentSystem : EntitySystem
{
    [Dependency] private readonly SharedInstrumentSystem _sharedInstrument = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SwappableInstrumentComponent, GetVerbsEvent<AlternativeVerb>>(AddStyleVerb);
    }

    private void AddStyleVerb(EntityUid uid, SwappableInstrumentComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || component.InstrumentList.Count <= 1)
            return;

        if (!TryComp<SharedInstrumentComponent>(uid, out var instrument))
            return;

        if (instrument.Playing) //no changing while playing
            return;

        var priority = 0;
        foreach (var entry in component.InstrumentList)
        {
            AlternativeVerb selection = new()
            {
                Text = entry.Key,
                Category = VerbCategory.InstrumentStyle,
                Priority = priority,
                Act = () =>
                {
                    _sharedInstrument.SetInstrumentProgram(instrument, entry.Value);
                    _popup.PopupEntity(Loc.GetString("swappable-instrument-component-style-set", ("style", entry.Key)),
                        args.User, Filter.Entities(args.User));
                }
            };

            priority--;
            args.Verbs.Add(selection);
        }
    }
}
