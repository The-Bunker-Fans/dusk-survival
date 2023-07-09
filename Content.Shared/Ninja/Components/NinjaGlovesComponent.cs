using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.DoAfter;
using Content.Shared.Ninja.Systems;
using Content.Shared.Toggleable;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using System.Threading;

namespace Content.Shared.Ninja.Components;

/// <summary>
/// Component for toggling glove powers.
/// Powers being enabled is controlled by User not being null.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedNinjaGlovesSystem))]
public sealed partial class NinjaGlovesComponent : Component
{
    /// <summary>
    /// Entity of the ninja using these gloves, usually means enabled
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? User;

    /// <summary>
    /// The action for toggling ninja gloves abilities
    /// </summary>
    [DataField("toggleAction")]
    public InstantAction ToggleAction = new()
    {
        DisplayName = "action-name-toggle-ninja-gloves",
        Description = "action-desc-toggle-ninja-gloves",
        Priority = -13,
        Event = new ToggleActionEvent()
    };

    /// <summary>
    /// The whitelist used for the emag provider to emag doors only.
    /// </summary>
    [DataField("doorjackWhitelist")]
    public EntityWhitelist DoorjackWhitelist = new()
    {
        Components = new[] {"Door"}
    };
}

/// <summary>
/// Component for stunning mobs on click, when gloves are enabled.
/// Knocks them down for a bit and deals shock damage.
/// </summary>
[RegisterComponent]
public sealed class NinjaStunComponent : Component
{
    /// <summary>
    /// Joules required in the suit to stun someone. Defaults to 10 uses on a small battery.
    /// </summary>
    [DataField("stunCharge")]
    public float StunCharge = 36.0f;

    /// <summary>
    /// Shock damage dealt when stunning someone
    /// </summary>
    [DataField("stunDamage")]
    public int StunDamage = 5;

    /// <summary>
    /// Time that someone is stunned for, stacks if done multiple times.
    /// </summary>
    [DataField("stunTime")]
    public TimeSpan StunTime = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Component for downloading research nodes from a R&D server, when gloves are enabled.
/// Requirement for greentext.
/// </summary>
[RegisterComponent]
public sealed class NinjaDownloadComponent : Component
{
    /// <summary>
    /// Time taken to download research from a server
    /// </summary>
    [DataField("downloadTime")]
    public float DownloadTime = 20f;
}


/// <summary>
/// Component for hacking a communications console to call in a threat.
/// Called threat is rolled from the ninja gamerule config.
/// </summary>
[RegisterComponent]
public sealed class NinjaTerrorComponent : Component
{
    /// <summary>
    /// Time taken to hack the console
    /// </summary>
    [DataField("terrorTime")]
    public float TerrorTime = 20f;
}

/// <summary>
/// DoAfter event for drain ability.
/// </summary>
[Serializable, NetSerializable]
public sealed class DrainDoAfterEvent : SimpleDoAfterEvent { }

/// <summary>
/// DoAfter event for research download ability.
/// </summary>
[Serializable, NetSerializable]
public sealed class DownloadDoAfterEvent : SimpleDoAfterEvent { }

/// <summary>
/// DoAfter event for comms console terror ability.
/// </summary>
[Serializable, NetSerializable]
public sealed class TerrorDoAfterEvent : SimpleDoAfterEvent { }
