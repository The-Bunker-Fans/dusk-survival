﻿using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Components;
using Content.Shared.Medical.Wounds.Prototypes;
using Content.Shared.Rejuvenate;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Medical.Wounds.Systems;

public sealed partial class WoundSystem : EntitySystem
{
    private const string WoundContainerId = "WoundSystemWounds";

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private readonly Dictionary<string, WoundTable> _cachedWounds = new();

    public override void Initialize()
    {
        CacheWoundData();
        _prototypeManager.PrototypesReloaded += _ => CacheWoundData();

        SubscribeLocalEvent<WoundableComponent, ComponentGetState>(OnWoundableGetState);
        SubscribeLocalEvent<WoundableComponent, ComponentHandleState>(OnWoundableHandleState);
        SubscribeLocalEvent<WoundableComponent, ComponentInit>(OnWoundableInit);

        SubscribeLocalEvent<WoundComponent, ComponentGetState>(OnWoundGetState);
        SubscribeLocalEvent<WoundComponent, ComponentHandleState>(OnWoundHandleState);

        SubscribeLocalEvent<BodyComponent, DamageChangedEvent>(OnBodyDamaged);
        SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnBodyRejuvenate);
    }

    private void OnWoundableInit(EntityUid uid, WoundableComponent component, ComponentInit args)
    {
        if (component.Health <= 0)
            component.Health = component.MaxIntegrity;
        if (component.Integrity <= 0)
            component.Integrity = component.MaxIntegrity;
    }

    private void OnBodyRejuvenate(EntityUid uid, BodyComponent component, RejuvenateEvent args)
    {
        HealAllWounds(uid, component);
        //TODO: reset bodypart health/structure values
    }

    private void OnWoundableGetState(EntityUid uid, WoundableComponent woundable, ref ComponentGetState args)
    {
        args.State = new WoundableComponentState(
            woundable.AllowedTraumaTypes,
            woundable.TraumaResistance,
            woundable.TraumaPenResistance,
            woundable.Health,
            woundable.HealthCap,
            woundable.HealthCapDamage,
            woundable.BaseHealingRate,
            woundable.HealingModifier,
            woundable.HealingMultiplier,
            woundable.Integrity,
            woundable.MaxIntegrity,
            woundable.DestroyWoundId
        );
    }

    private void OnWoundableHandleState(EntityUid uid, WoundableComponent woundable, ref ComponentHandleState args)
    {
        if (args.Current is not WoundableComponentState state)
            return;

        woundable.AllowedTraumaTypes = state.AllowedTraumaTypes;
        woundable.TraumaResistance = state.TraumaResistance;
        woundable.TraumaPenResistance = state.TraumaPenResistance;
        woundable.Health = state.Health;
        woundable.HealthCap = state.HealthCap;
        woundable.HealthCapDamage = state.HealthCapDamage;
        woundable.BaseHealingRate = state.BaseHealingRate;
        woundable.HealingModifier = state.HealingModifier;
        woundable.HealingMultiplier = state.HealingMultiplier;
        woundable.Integrity = state.Integrity;
        woundable.MaxIntegrity = state.MaxIntegrity;
        woundable.DestroyWoundId = state.DestroyWoundId;
    }

    private void OnWoundGetState(EntityUid uid, WoundComponent wound, ref ComponentGetState args)
    {
        args.State = new WoundComponentState(
            wound.ScarWound,
            wound.HealthCapDamage,
            wound.IntegrityDamage,
            wound.Severity,
            wound.BaseHealingRate,
            wound.HealingModifier,
            wound.HealingMultiplier,
            wound.CanBleed,
            wound.ValidTreatments
        );
    }

    private void OnWoundHandleState(EntityUid uid, WoundComponent wound, ref ComponentHandleState args)
    {
        if (args.Current is not WoundComponentState state)
            return;

        wound.ScarWound = state.ScarWound;
        wound.HealthCapDamage = state.HealthCapDamage;
        wound.IntegrityDamage = state.IntegrityDamage;
        wound.Severity = state.Severity;
        wound.BaseHealingRate = state.BaseHealingRate;
        wound.HealingModifier = state.HealingModifier;
        wound.HealingMultiplier = state.HealingMultiplier;
        wound.CanBleed = state.CanBleed;
        wound.ValidTreatments = state.ValidTreatments;
    }

    private void CacheWoundData()
    {
        _cachedWounds.Clear();

        foreach (var traumaType in _prototypeManager.EnumeratePrototypes<TraumaPrototype>())
        {
            _cachedWounds.Add(traumaType.ID, new WoundTable(traumaType));
        }
    }

    public override void Update(float frameTime)
    {
        UpdateHealing(frameTime);
    }

    private void OnBodyDamaged(EntityUid uid, BodyComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;
        //TODO targeting
        var parts = _body.GetBodyChildren(uid, component).ToList();
        var part = _random.Pick(parts);
        TryApplyTrauma(args.Origin, part.Id, args.DamageDelta);
    }

    private readonly struct WoundTable
    {
        private readonly SortedDictionary<FixedPoint2, string> _wounds;

        public WoundTable(TraumaPrototype trauma)
        {
            _wounds = trauma.Wounds;
        }

        public IEnumerable<KeyValuePair<FixedPoint2, string>> HighestToLowest()
        {
            return _wounds.Reverse();
        }
    }
}
