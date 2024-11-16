namespace PLang.Utils;

public static class DateTimeExtension
{
    public static long GetUnixTime(this DateTime dateTime)
    {
        return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
    }
}