using System.ComponentModel;

namespace PLang.Building.Model;

public class CachingHandler
{
    [Attributes.DefaultValue(50)] public long TimeInMilliseconds { get; set; }

    [Attributes.DefaultValue(null)] public string? CacheKey { get; set; } = null;

    [Attributes.DefaultValue(0)]
    [Description("Sliding = 0, Absolute = 1")]
    public int CachingType { get; set; } = 0;
}