#nullable enable
using System;
using Content.Shared.NetIDs;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Suspicion
{
    [NetID(ContentNetIDs.SUSPICION_ROLE)]
    public abstract class SharedSuspicionRoleComponent : Component
    {
        public sealed override string Name => "SuspicionRole";
    }

    [Serializable, NetSerializable]
    public class SuspicionRoleComponentState : ComponentState
    {
        public readonly string? Role;
        public readonly bool? Antagonist;
        public readonly (string name, EntityUid)[] Allies;

        public SuspicionRoleComponentState(string? role, bool? antagonist, (string name, EntityUid)[] allies)
        {
            Role = role;
            Antagonist = antagonist;
            Allies = allies;
        }
    }
}
