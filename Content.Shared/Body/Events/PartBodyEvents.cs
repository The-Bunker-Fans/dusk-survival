﻿using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;

namespace Content.Shared.Body.Events;

/// <summary>
///     Raised on a body part and the body when added to a body.
/// </summary>
public sealed class PartAddedToBodyEvent : EntityEventArgs
{
    public readonly SharedBodyPartComponent Part;
    public readonly BodyPartSlot Slot;

    public readonly SharedBodyComponent? OldBody;
    public readonly SharedBodyComponent NewBody;

    public PartAddedToBodyEvent(SharedBodyPartComponent part, SharedBodyComponent newBody, BodyPartSlot slot, SharedBodyComponent? oldBody=null)
    {
        Slot = slot;
        Part = part;
        OldBody = oldBody;
        NewBody = newBody;
    }
}

/// <summary>
///     Raised on a body part and the body when removed from a body.
/// </summary>
public sealed class PartRemovedFromBodyEvent : EntityEventArgs
{
    public readonly SharedBodyPartComponent Part;
    public readonly BodyPartSlot Slot;

    public readonly SharedBodyComponent OldBody;
    public readonly SharedBodyComponent? NewBody;

    public PartRemovedFromBodyEvent(SharedBodyPartComponent part, SharedBodyComponent oldBody, BodyPartSlot slot, SharedBodyComponent? newBody=null)
    {
        Slot = slot;
        Part = part;
        OldBody = oldBody;
        NewBody = newBody;
    }
}
