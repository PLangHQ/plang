namespace PLang.Runtime2.modules.mock.types;

public class MockHandle
{
    public string Id { get; init; } = "";
    public string ActionPattern { get; init; } = "";
    public int CallCount => Calls.Count;
    public List<MockCall> Calls { get; } = new();
    public string EventBindingId { get; set; } = "";
    public bool IsSpy { get; init; }

    public void RecordCall(Dictionary<string, object?> parameters)
    {
        Calls.Add(new MockCall
        {
            Parameters = parameters,
            Timestamp = DateTime.UtcNow
        });
    }
}

public class MockCall
{
    public Dictionary<string, object?> Parameters { get; init; } = new();
    public DateTime Timestamp { get; init; }
}
