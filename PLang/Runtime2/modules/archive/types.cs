namespace PLang.Runtime2.modules.archive;

/// <summary>
/// Result types for archive module actions.
/// </summary>
public static class types
{
    public record settingsResult
    {
        public long? max { get; init; }
        public string? level { get; init; }
        public bool isDefault { get; init; }
    }
}
