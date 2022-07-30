namespace Content.Server.AI.HTN;

/// <summary>
/// AKA Method. This is a branch available for a compound task.
/// </summary>
[DataDefinition]
public sealed class HTNBranch
{
    /// <summary>
    /// A debug friendly name for the branch.
    /// </summary>
    [ViewVariables, DataField("name")] public string Name = string.Empty;

    // Made this its own class if we ever need to change it.
    [ViewVariables, DataField("preconditions")]
    public List<HTNPrecondition> Preconditions = new();

    [ViewVariables, DataField("tasks", required: true, customTypeSerializer:typeof(HTNTaskListSerializer))]
    public List<HTNTask> Tasks = default!;
}
