namespace app.mock.Mock;

/// <summary>
/// Handle for an active mock intercept registered via <c>mock.intercept</c>.
/// Tracks the actions matched and the calls captured during the test run.
/// Consumed by <c>mock.reset</c> (to tear down the binding) and
/// <c>mock.verify</c> (to assert call shape).
/// </summary>
public class @this
{
    public string Id { get; init; } = "";
    public string ActionPattern { get; init; } = "";
    public int CallCount => Calls.Count;
    public bool IsSpy { get; init; }
    public List<Call> Calls { get; } = new();
    public string EventBindingId { get; set; } = "";

    public void RecordCall(Dictionary<string, object?> parameters)
    {
        Calls.Add(new Call
        {
            Parameters = parameters,
            Timestamp = DateTime.UtcNow
        });
    }
}

public class Call
{
    public Dictionary<string, object?> Parameters { get; init; } = new();
    public DateTime Timestamp { get; init; }
}
