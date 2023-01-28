namespace Content.Server.Traits.Assorted;

[RegisterComponent]
public sealed class ParacusiaComponent : Component
{
    /// <summary>
    /// The random time between incidents, (min, max).
    /// </summary>
    [DataField("maxTimeBetweenIncidents", required: true)]
    public float maxTimeBetweenIncidents { get; }

    [DataField("minTimeBetweenIncidents", required: true)]
    public float minTimeBetweenIncidents { get; }

    /// <summary>
    /// How far away at most can the sound be?
    /// </summary>
    [DataField("maxSoundDistance", required: true)]
    public float MaxSoundDistance { get; }
    /// <summary>
    /// The sounds to choose from
    /// </summary
    [DataField("sounds", required: true)]
    public List<string>? Sounds { get; }

    public float NextIncidentTime;
}
