using Content.Server.Power.NodeGroups;
using Content.Shared.APC;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Power.Components;

[RegisterComponent]
public sealed partial class ApcComponent : BaseApcNetComponent
{
    [DataField("onReceiveMessageSound")]
    public SoundSpecifier OnReceiveMessageSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    [DataField("lastChargeState")]
    public ApcChargeState LastChargeState;
    [DataField("lastChargeStateTime", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastChargeStateTime;

    [DataField("lastExternalState")]
    public ApcExternalPowerState LastExternalState;
    [DataField("lastUiUpdate", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastUiUpdate;

    [DataField("enabled")]
    public bool MainBreakerEnabled = true;
    // TODO: remove this since it probably breaks when 2 people use it
    [DataField("hasAccess")]
    public bool HasAccess = false;

    public const float HighPowerThreshold = 0.9f;
    public static TimeSpan VisualsChangeDelay = TimeSpan.FromSeconds(1);

    // TODO ECS power a little better!
    protected override void AddSelfToNet(IApcNet apcNet)
    {
        apcNet.AddApc(this);
    }

    protected override void RemoveSelfFromNet(IApcNet apcNet)
    {
        apcNet.RemoveApc(this);
    }
}
