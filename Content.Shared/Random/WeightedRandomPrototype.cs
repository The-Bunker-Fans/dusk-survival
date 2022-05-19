using Robust.Shared.Prototypes;

namespace Content.Shared.Random;

[Prototype("weightedRandom")]
public sealed class WeightedRandomPrototype : IPrototype
{
    [IdDataFieldAttribute] public string ID { get; } = default!;

    [DataField("weights")]
    public Dictionary<string, float> Weights = new();
}
