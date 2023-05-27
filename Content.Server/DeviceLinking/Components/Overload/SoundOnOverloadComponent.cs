﻿using Content.Server.DeviceLinking.Systems;
using Robust.Shared.Audio;

namespace Content.Server.DeviceLinking.Components.Overload;

/// <summary>
/// Plays a sound when a device link overloads
/// </summary>
[RegisterComponent]
[Access(typeof(DeviceLinkOverloadSystem))]
public sealed class SoundOnOverloadComponent : Component
{
    /// <summary>
    /// Sound to play when the device overloads
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? OverloadSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg");

    /// <summary>
    /// Modifies the volume the sound is played at
    /// </summary>
    [DataField("volumeModifier")]
    public float VolumeModifier;
}
