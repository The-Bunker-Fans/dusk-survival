﻿using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Mobs.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(MobThresholdSystem))]
public sealed class MobThresholdComponent : Component
{
    [DataField("thresholds", required:true)]public SortedDictionary<FixedPoint2, MobState> Thresholds = new();

    public Dictionary<MobState, FixedPoint2> ThresholdReverseLookup = new();

    public MobState CurrentThresholdState;
}

[Serializable, NetSerializable]
public sealed class MobThresholdComponentState : ComponentState
{
    public Dictionary<FixedPoint2, MobState> Thresholds;
    public MobState CurrentThresholdState;
    public MobThresholdComponentState(MobState currentThresholdState,
        Dictionary<FixedPoint2, MobState> thresholds)
    {
        CurrentThresholdState = currentThresholdState;
        Thresholds = thresholds;
    }

}
