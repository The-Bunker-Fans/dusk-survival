using Content.Shared.Thief;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Thief.Components;

/// <summary>
/// This component stores the possible contents of the backpack,
/// which can be selected via the interface.
/// </summary>
[RegisterComponent]
public sealed partial class ThiefUndeterminedBackpackComponent : Component
{
    /// <summary>
    /// List of sets available for selection
    /// </summary>
    [DataField]
    public List<ProtoId<ThiefBackpackSetPrototype>> PossibleSets = new();

    [DataField]
    public List<int> SelectedSets = new();

    [DataField]
    public SoundSpecifier ApproveSound = new SoundPathSpecifier("/Audio/Effects/rustle1.ogg");
}
