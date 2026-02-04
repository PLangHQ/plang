namespace PLang.Runtime;

/// <summary>
/// Data transfer object for deserializing .pr files
/// </summary>
internal class GoalData
{
    public string Path { get; set; } = "";
    public List<StepData> Steps { get; set; } = new();
}

/// <summary>
/// Data transfer object for step data in .pr files
/// </summary>
internal class StepData
{
    public int Line { get; set; }
    public string Text { get; set; } = "";
    public string Module { get; set; } = "";
    public string Method { get; set; } = "";
}
