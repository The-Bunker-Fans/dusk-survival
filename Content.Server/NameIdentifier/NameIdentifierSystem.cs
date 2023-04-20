﻿using System.Linq;
using Content.Shared.GameTicking;
using Content.Shared.NameIdentifier;
using Robust.Shared.Collections;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.NameIdentifier;

/// <summary>
///     Handles unique name identifiers for entities e.g. `monkey (MK-912)`
/// </summary>
public sealed class NameIdentifierSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;

    /// <summary>
    /// Free IDs available per <see cref="NameIdentifierGroupPrototype"/>.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, List<int>> CurrentIds = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NameIdentifierComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<NameIdentifierComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(CleanupIds);

        InitialSetupPrototypes();
        _prototypeManager.PrototypesReloaded += OnReloadPrototypes;
    }

    private void OnComponentRemove(EntityUid uid, NameIdentifierComponent component, ComponentRemove args)
    {
        if (CurrentIds.TryGetValue(component.Group, out var ids))
        {
            // Avoid inserting the value right back at the end or shuffling in place:
            // just pick a random spot to put it and then move that one to the end.
            var randomIndex = _robustRandom.Next(ids.Count);
            var random = ids[randomIndex];
            ids[randomIndex] = component.Identifier;
            ids.Add(random);
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _prototypeManager.PrototypesReloaded -= OnReloadPrototypes;
    }

    /// <summary>
    ///     Generates a new unique name/suffix for a given entity and adds it to <see cref="CurrentIds"/>
    ///     but does not set the entity's name.
    /// </summary>
    public string GenerateUniqueName(EntityUid uid, NameIdentifierGroupPrototype proto, out int randomVal)
    {
        randomVal = 0;
        var entityName = Name(uid);
        if (!CurrentIds.TryGetValue(proto.ID, out var set))
            return entityName;
        
        if (set.Count == 0)
        {
            // Oh jeez. We're outta numbers.
            return entityName;
        }

        randomVal = set[^1];
        set.RemoveAt(set.Count - 1);

        return proto.Prefix is not null
            ? $"{proto.Prefix}-{randomVal}"
            : $"{randomVal}";
    }

    private void OnComponentInit(EntityUid uid, NameIdentifierComponent component, ComponentInit args)
    {
        if (!_prototypeManager.TryIndex<NameIdentifierGroupPrototype>(component.Group, out var group))
            return;

        int id;
        string uniqueName;

        // If it has an existing valid identifier then use that, otherwise generate a new one.
        if (component.Identifier != -1 &&
            CurrentIds.TryGetValue(component.Group, out var ids) &&
            ids.Remove(component.Identifier))
        {
            id = component.Identifier;
            uniqueName = group.Prefix is not null
                ? $"{group.Prefix}-{id}"
                : $"{id}";
        }
        else
        {
            uniqueName = GenerateUniqueName(uid, group, out id);
            component.Identifier = id;
        }

        var meta = MetaData(uid);
        // "DR-1234" as opposed to "drone (DR-1234)"
        meta.EntityName = group.FullName
            ? uniqueName
            : $"{meta.EntityName} ({uniqueName})";
    }

    private void InitialSetupPrototypes()
    {
        foreach (var proto in _prototypeManager.EnumeratePrototypes<NameIdentifierGroupPrototype>())
        {
            AddGroup(proto);
        }
    }

    private void AddGroup(NameIdentifierGroupPrototype proto)
    {
        var values = new List<int>(proto.MaxValue - proto.MinValue);

        for (var i = proto.MinValue; i < proto.MaxValue; i++)
        {
            values.Add(i);
        }

        _robustRandom.Shuffle(values);
        CurrentIds.Add(proto.ID, values);
    }

    private void OnReloadPrototypes(PrototypesReloadedEventArgs ev)
    {
        if (!ev.ByType.TryGetValue(typeof(NameIdentifierGroupPrototype), out var set))
            return;

        var toRemove = new ValueList<string>();

        foreach (var proto in CurrentIds.Keys)
        {
            if (!_prototypeManager.HasIndex<NameIdentifierGroupPrototype>(proto))
            {
                toRemove.Add(proto);
            }
        }

        foreach (var proto in toRemove)
        {
            CurrentIds.Remove(proto);
        }

        foreach (var proto in set.Modified.Values)
        {
            // Only bother adding new ones.
            if (CurrentIds.ContainsKey(proto.ID))
                continue;

            AddGroup((NameIdentifierGroupPrototype) proto);
        }
    }

    private void CleanupIds(RoundRestartCleanupEvent ev)
    {
        foreach (var values in CurrentIds.Values)
        {
            _robustRandom.Shuffle(values);
        }
    }
}
