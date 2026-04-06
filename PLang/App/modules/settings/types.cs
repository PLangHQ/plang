namespace App.modules.settings;

public static class types
{
    public record setting
    {
        public string? key { get; init; }
        public object? value { get; init; }
    }
}
