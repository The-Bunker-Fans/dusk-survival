#nullable enable
using System;
using Content.Shared.NetIDs;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.Camera
{
    [NetID(ContentNetIDs.CAMERA_RECOIL)]
    public abstract class SharedCameraRecoilComponent : Component
    {
        public sealed override string Name => "CameraRecoil";

        public abstract void Kick(Vector2 recoil);

        [Serializable, NetSerializable]
        protected class RecoilKickMessage : ComponentMessage
        {
            public readonly Vector2 Recoil;

            public RecoilKickMessage(Vector2 recoil)
            {
                Directed = true;
                Recoil = recoil;
            }
        }
    }
}
