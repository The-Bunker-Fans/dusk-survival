#nullable enable
using System;
using Content.Shared.NetIDs;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Flash
{
    [NetID(ContentNetIDs.FLASHABLE)]
    public class SharedFlashableComponent : Component
    {
        public override string Name => "Flashable";
    }

    [Serializable, NetSerializable]
    public class FlashComponentState : ComponentState
    {
        public double Duration { get; }
        public TimeSpan Time { get; }

        public FlashComponentState(double duration, TimeSpan time)
        {
            Duration = duration;
            Time = time;
        }
    }
}
