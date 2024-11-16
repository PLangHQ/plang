namespace PLang.Utils;

public static class SystemTime
{
    public static Func<DateTime> Now = () => DateTime.Now;
    public static Func<DateTime> UtcNow = () => DateTime.UtcNow;
    public static Func<DateTimeOffset> OffsetNow = () => DateTimeOffset.Now;
    public static Func<DateTimeOffset> OffsetUtcNow = () => DateTimeOffset.UtcNow;
}

public static class SystemNonce
{
    public static Func<string> New = () => Guid.NewGuid().ToString();
}