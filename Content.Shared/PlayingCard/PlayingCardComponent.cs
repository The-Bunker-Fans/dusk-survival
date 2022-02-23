using Robust.Shared.Serialization;
using Robust.Shared.GameStates;


namespace Content.Shared.PlayingCard
{
    [RegisterComponent]
    public sealed class PlayingCardComponent : Component, ISerializationHooks
    {
        [ViewVariables(VVAccess.ReadWrite)]
        // [DataField("stackType", required:true, customTypeSerializer:typeof(PrototypeIdSerializer<StackPrototype>))]
        [DataField("stackId")]
        public string StackTypeId { get; private set; } = string.Empty;

        [DataField("cardName")]
        public string CardName = "Playing Card";
        [DataField("cardDescription")]
        public string CardDescription = "a playing card";
        [DataField("isFacingUp")]
        public bool FacingUp = false;
        [DataField("cardHandPrototype")]
        public string CardHandPrototype { get; private set; } = string.Empty;
    }
}
