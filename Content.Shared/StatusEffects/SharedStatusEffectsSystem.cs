using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffects.Components;
using Content.Shared.StatusEffects.Prototypes;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.StatusEffects;

/// <summary>
/// The skeleton of status effects.
/// </summary>
public abstract partial class SharedStatusEffectsSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly MobThresholdSystem _thresholdSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StatusEffectsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StatusEffectComponent, ComponentStartup>(OnEffectStartup);

        // Event relays down here
        SubscribeLocalEvent<StatusEffectsComponent, MeleeHitEvent>(RelayEvent);
        SubscribeLocalEvent<StatusEffectsComponent, BeforeDamageChangedEvent>(RefRelayEvent);
        SubscribeLocalEvent<StatusEffectsComponent, DamageModifyEvent>(RelayEvent);
        SubscribeLocalEvent<StatusEffectsComponent, DamageChangedEvent>(RelayEvent);
        SubscribeLocalEvent<StatusEffectsComponent, GetStatusIconsEvent>(RefRelayEvent);

        // InitializeActivation();
        // InitializeEffects();
    }

    #region Events

    /// <summary>
    /// The entire stat effect economy will collapse without this.
    /// </summary>
    private void OnStartup(EntityUid uid, StatusEffectsComponent component, ComponentStartup args)
    {
        component.StatusContainer = _container.EnsureContainer<Container>(uid, component.StatusContainerId);
        component.StatusContainer.OccludesLight = false;
    }

    private void OnEffectStartup(EntityUid uid, StatusEffectComponent comp, ComponentStartup args)
    {
        comp.EndTime = Timing.CurTime + TimeSpan.FromSeconds(comp.DefaultLength);
    }

    #endregion

    #region Funcitons

    /// <summary>
    /// Used to apply a status effect onto an entity. It should first check if the entity should have the effect anyways. Returns null/the effect entity when successfully applied.
    /// </summary>
    /// <param name="effectApplyType">Use the EffectModifyMode enum.</param>
    public EntityUid? TryApplyStatusEffect(
        EntityUid uid,
        string effectID,
        int stacks,
        TimeSpan? length = null,
        EffectModifyMode effectApplyType = EffectModifyMode.Override,
        StatusEffectsComponent? comp = null)
    {
        if (!Resolve(uid, ref comp) || comp.StatusContainer == null)
            return null;

        if (comp.EffectsWhitelist != null)
        {
            if (!_protoManager.TryIndex<StatusEffectWhitelistPrototype>(comp.EffectsWhitelist, out var whitelist)) // In case of misspelling, don't leave the coder in shambles trying to figure out what went wrong.
            {
                _sawmill.Warning($"The effect whitelist prototype '{comp.EffectsWhitelist}' does not exist. Have you tried creating one?");
                return null;
            }
            if (!whitelist.Effects.Any(effect => effect == effectID))
                return null;
        }

        var effect = ApplyStatusEffect(uid, effectID, stacks, length, effectApplyType, comp);
        return effect;
    }

    /// <summary>
    /// Used to apply a status effect onto an entity. Returns null/the effect entity when successfully applied.
    /// </summary>
    /// <param name="effectApplyType">Use the EffectModifyMode enum.</param>
    public EntityUid? ApplyStatusEffect(
        EntityUid uid,
        string effectID,
        int stacks,
        TimeSpan? length = null,
        EffectModifyMode effectApplyType = EffectModifyMode.Override,
        StatusEffectsComponent? comp = null)
    {
        if (!Resolve(uid, ref comp) || comp.StatusContainer == null || stacks <= 0)
            return null;

        if (!PrototypeManager.TryIndex<EntityPrototype>(effectID, out var effectPrototype))
        {
            Log.Error($"Entity prototype of '{effectID}' could not be found.");
            return null;
        }

        foreach (var storedEffect in comp.StatusContainer.ContainedEntities)
        {
            if (TryComp<StatusEffectComponent>(storedEffect, out var effectComp) &&
                TryComp<MetaDataComponent>(storedEffect, out var metaData) && metaData.EntityPrototype == effectPrototype)
            {
                ModifyEffect(storedEffect, stacks, length, effectApplyType, effectComp);

                return storedEffect;
            }
        }

        var effect = Spawn(effectID, Transform(uid).Coordinates);
        ModifyEffect(effect, stacks, length, EffectModifyMode.Override);

        comp.StatusContainer.Insert(effect);

        return effect;
    }

    public void ModifyEffect(
        EntityUid uid,
        int newStacks,
        TimeSpan? newLength = null,
        EffectModifyMode effectApplyType = EffectModifyMode.Override,
        StatusEffectComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        var curTime = Timing.CurTime;
        newStacks = ModifyStacks(newStacks, comp.MaxStacks);

        switch (effectApplyType)
        {
            case EffectModifyMode.Override:
                comp.Stacks = newStacks;
                if (newLength != null)
                    comp.EndTime = curTime + newLength.Value;
                break;
            case EffectModifyMode.UseStrongest:
                if (newStacks < comp.Stacks)
                    break;
                comp.Stacks = newStacks;

                if (newLength != null && curTime + newLength.Value <= comp.EndTime)
                    comp.EndTime = curTime + newLength.Value;
                break;
            case EffectModifyMode.AddTime:
                if (newLength == null)
                    break;

                if (newStacks > comp.Stacks)
                {
                    comp.EndTime = curTime + newLength.Value + (comp.EndTime - curTime) * (comp.Stacks / newStacks);
                    comp.Stacks = newStacks;
                }
                else
                    comp.EndTime = curTime + (comp.EndTime - curTime) + newLength.Value * (newStacks / comp.Stacks);
                break;
            case EffectModifyMode.AddStacks:
                comp.Stacks = ModifyStacks(comp.Stacks + newStacks, comp.MaxStacks);
                break;
            default:
                _sawmill.Warning($"'{effectApplyType}'is not an actual apply type.");
                break;
        }
    }

    private static int ModifyStacks(int stacks, int maxStacks)
    {
        if (maxStacks > 0)
            return Math.Clamp(stacks, 0, maxStacks);
        else
            return stacks;
    }

    #endregion

    #region Relays

    /// <summary>
    /// Used to relay an event that an entity recieved into it's effects so that the event can be modified by the effects.
    /// </summary>
    private void RelayEvent<TEvent>(EntityUid uid, StatusEffectsComponent comp, TEvent args)
    {
        var relayedArgs = new StatusEffectRelayEvent<TEvent>(args, uid);

        if (comp.StatusContainer == null)
            return;

        foreach (var effect in comp.StatusContainer.ContainedEntities)
        {
            RaiseLocalEvent(effect, relayedArgs);
        }
    }

    /// <summary>
    /// A ref version of RelayEvent, which is used to relay an event that an entity recieved into it's effects.
    /// </summary>
    private void RefRelayEvent<TEvent>(EntityUid uid, StatusEffectsComponent comp, ref TEvent args)
    {
        var relayedArgs = new StatusEffectRelayEvent<TEvent>(args, uid);

        if (comp.StatusContainer == null)
            return;

        foreach (var effect in comp.StatusContainer.ContainedEntities)
        {
            RaiseLocalEvent(effect, relayedArgs);
        }
    }

    #endregion
}


public enum EffectModifyMode
{
    Override, // Should completely override the effect.
    UseStrongest, // Uses the highest stack effect, then uses the one with the highest length.
    AddTime, // Adds the time of the two effects together, considering their stack sizes. It will also override
    AddStacks, // Adds the stacks together. Doesn't modify the time.
}
