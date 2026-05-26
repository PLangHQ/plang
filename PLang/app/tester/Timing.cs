namespace app.tester;

/// <summary>
/// Wall-clock duration of a single step in a test's entry goal.
/// Step text is intentionally absent — webui resolves it by index against
/// the goal source (single source of truth).
/// </summary>
public sealed record Timing(int StepIndex, double Ms);
