namespace Bench.mode;

/// <summary>One measurement: it runs against the apps the setting asks for and says what it found, one line of
/// `key=value` pairs for the ones <c>run.sh</c> reads.</summary>
internal abstract class @this
{
    protected readonly setting.@this Setting;
    protected readonly born.@this Born;

    protected @this(setting.@this setting)
    {
        Setting = setting;
        Born = new(setting);
    }

    /// <summary>Measures and says; the exit code (0 when it ran clean).</summary>
    internal abstract Task<int> Run();

    /// <summary>One line on stdout, numbers written the same in every culture.</summary>
    protected static void Say(FormattableString line) => Console.WriteLine(FormattableString.Invariant(line));
}
