using Robust.Shared.Audio;

namespace Content.Server.Flash.Components
{
    [RegisterComponent, Access(typeof(FlashSystem))]
    public sealed partial class FlashComponent : Component
    {

        [DataField("duration")]
        [ViewVariables(VVAccess.ReadWrite)]
        public int FlashDuration { get; set; } = 5000;

        [DataField("range")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float Range { get; set; } = 7f;

        /// <summary>
        /// If set, the flash can be used for AoE in hand.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("aoeFlashDuration")]
        public int? AoeFlashDuration { get; set; } = 2000;

        [DataField("slowTo")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float SlowTo { get; set; } = 0.5f;

        /// <summary>
        /// If set, the flash will also knock down and stun the target.
        /// </summary>
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public float? StunTime = null;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("sound")]
        public SoundSpecifier Sound { get; set; } = new SoundPathSpecifier("/Audio/Weapons/flash.ogg")
        {
            Params = AudioParams.Default.WithVolume(1f).WithMaxDistance(3f)
        };

        public bool Flashing;
    }
}
