using Content.Server.Thief.Systems;
using Content.Shared.Communications;
using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Content.Server.StationEvents.Events;
using Content.Shared.Roles;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Player;
using Content.Shared.Preferences;

namespace Content.Server.GameTicking.Rules.Components;

/// <summary>
/// Stores data for <see cref="ThiefRuleSystem/">.
/// </summary>
[RegisterComponent, Access(typeof(ThiefRuleSystem))]
public sealed partial class ThiefRuleComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<AntagPrototype>))]
    public string ThiefPrototypeId = "Thief";

    public Dictionary<ICommonSession, HumanoidCharacterProfile> StartCandidates = new();

    [DataField]
    public float MaxObjectiveDifficulty = 3f;

    [DataField]
    public int MaxStealObjectives = 10;

    /// <summary>
    /// All Thiefes created by this rule
    /// </summary>
    public readonly List<EntityUid> ThiefMinds = new();

    /// <summary>
    /// Max Thiefs created by rule
    /// </summary>
    public int MaxAllowThief = 2;

    /// <summary>
    /// Sound played when making the player a thief via antag control or ghost role
    /// </summary>
    [DataField]
    public SoundSpecifier? GreetingSound = new SoundPathSpecifier("/Audio/Misc/thief_greeting.ogg"); //TO DO COOL SOUND
}
