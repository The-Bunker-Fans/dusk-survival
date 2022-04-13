﻿namespace Content.Server.Sticky.Events;

/// <summary>
///     Risen on sticky entity when it was stuck to other entity.
/// </summary>
public sealed class EntityStuckEvent : EntityEventArgs
{
    /// <summary>
    ///     Entity that was used as a surface for sticky object.
    /// </summary>
    public readonly EntityUid Target;

    /// <summary>
    ///     Entity that stuck sticky object on target.
    /// </summary>
    public readonly EntityUid User;

    public EntityStuckEvent(EntityUid target, EntityUid user)
    {
        Target = target;
        User = user;
    }
}
