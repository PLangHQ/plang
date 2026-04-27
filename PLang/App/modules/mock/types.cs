using App.Attributes;

namespace App.modules.mock.types;

[PlangType("mockhandle")]
public class MockHandle
{
    [LlmBuilder] public string Id { get; init; } = "";
    [LlmBuilder] public string ActionPattern { get; init; } = "";
    [LlmBuilder] public int CallCount => Calls.Count;
    [LlmBuilder] public bool IsSpy { get; init; }
    public List<MockCall> Calls { get; } = new();
    public string EventBindingId { get; set; } = "";

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
