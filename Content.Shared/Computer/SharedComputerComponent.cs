using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Computer
{
    [Serializable, NetSerializable]
    public enum ComputerVisuals : byte
    {
        // Bool
        Powered,

        // Bool
        Broken
    }
}
