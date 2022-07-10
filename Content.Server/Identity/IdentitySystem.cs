﻿using Content.Server.Access.Systems;
using Content.Shared.CharacterAppearance.Components;
using Content.Shared.Identity;
using Content.Shared.Identity.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Preferences;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;

namespace Content.Server.Identity;

/// <summary>
///     Responsible for updating the identity of an entity on init or clothing equip/unequip.
/// </summary>
public class IdentitySystem : SharedIdentitySystem
{
    [Dependency] private readonly IdCardSystem _idCard = default!;

    private Queue<EntityUid> _queuedIdentityUpdates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdentityComponent, DidEquipEvent>(OnEquip);
        SubscribeLocalEvent<IdentityComponent, DidUnequipEvent>(OnUnequip);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        while (_queuedIdentityUpdates.TryDequeue(out var ent))
        {
            if (!TryComp<IdentityComponent>(ent, out var identity))
                continue;

            UpdateIdentityInfo(ent, identity);
        }
    }

    // This is where the magic happens
    protected override void OnComponentInit(EntityUid uid, IdentityComponent component, ComponentInit args)
    {
        base.OnComponentInit(uid, component, args);

        var ident = Spawn(null, Transform(uid).Coordinates);

        QueueIdentityUpdate(uid);
        component.IdentityEntitySlot.Insert(ident);
    }

    /// <summary>
    ///     Queues an identity update to the start of the next tick.
    /// </summary>
    public void QueueIdentityUpdate(EntityUid uid)
    {
        _queuedIdentityUpdates.Enqueue(uid);
    }

    private void OnEquip(EntityUid uid, IdentityComponent component, DidEquipEvent args)
    {
        QueueIdentityUpdate(uid);
    }

    private void OnUnequip(EntityUid uid, IdentityComponent component, DidUnequipEvent args)
    {
        QueueIdentityUpdate(uid);
    }

    #region Private API

    /// <summary>
    ///     Updates the metadata name for the id(entity) from the current state of the character.
    /// </summary>
    private void UpdateIdentityInfo(EntityUid uid, IdentityComponent identity)
    {
        if (identity.IdentityEntitySlot.ContainedEntity is not { } ident)
            return;

        // Clone the old entity's grammar to the identity entity, for loc purposes.
        if (TryComp<GrammarComponent>(uid, out var grammar))
        {
            var identityGrammar = EnsureComp<GrammarComponent>(ident);
            identityGrammar.Attributes.Clear();

            foreach (var (k, v) in grammar.Attributes)
            {
                identityGrammar.Attributes.Add(k, v);
            }
        }

        var name = GetIdentityName(uid);
        MetaData(ident).EntityName = name;
    }

    private string GetIdentityName(EntityUid target,
        InventoryComponent? inventory=null,
        HumanoidAppearanceComponent? appearance=null)
    {
        var representation = GetIdentityRepresentation(target, inventory, appearance);
        var ev = new SeeIdentityAttemptEvent();

        RaiseLocalEvent(target, ev);
        return representation.ToStringKnown(!ev.Cancelled);
    }

    /// <summary>
    ///     Gets an 'identity representation' of an entity, with their true name being the entity name
    ///     and their 'presumed name' and 'presumed job' being the name/job on their ID card, if they have one.
    /// </summary>
    private IdentityRepresentation GetIdentityRepresentation(EntityUid target,
        InventoryComponent? inventory=null,
        HumanoidAppearanceComponent? appearance=null)
    {
        int age = HumanoidCharacterProfile.MinimumAge;
        Gender gender = Gender.Neuter;

        // Always use their actual age and gender, since that can't really be changed by an ID.
        if (Resolve(target, ref appearance, false))
        {
            gender = appearance.Gender;
            age = appearance.Age;
        }

        var trueName = Name(target);
        if (!Resolve(target, ref inventory, false))
            return new(trueName, age, gender, string.Empty);

        string? presumedJob = null;
        string? presumedName = null;

        // Get their name and job from their ID for their presumed name.
        if (_idCard.TryFindIdCard(target, out var id))
        {
            presumedName = id.FullName;
            presumedJob = id.JobTitle?.ToLowerInvariant();
        }

        // If it didn't find a job, that's fine.
        return new(trueName, age, gender, presumedName, presumedJob);
    }

    #endregion
}
