namespace Bench.mode.list;

/// <summary>The modes a run can be asked for, by their word.</summary>
internal static class @this
{
    /// <summary>The mode <paramref name="setting"/> names; null when it names none.</summary>
    internal static mode.@this? Find(setting.@this setting) => setting.Mode switch
    {
        "birth" => new birth.@this(setting),
        "request" => new request.@this(setting),
        "steps" => new steps.@this(setting),
        "index" => new index.@this(setting),
        "events" => new events.@this(setting),
        "alloc" => new alloc.@this(setting),
        "phases" => new phases.@this(setting),
        "cold" => new cold.@this(setting),
        "loadtypes" => new loadtypes.@this(setting),
        "repeat" => new repeat.@this(setting),
        _ => null,
    };
}
