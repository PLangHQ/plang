namespace app.module.terminal.setting;

/// <summary>
/// How the terminal runs programs — <c>%!terminal%</c>. Every <c>terminal.start</c> reads these;
/// a step's own values win where it has them. <c>save %!terminal%</c> keeps them for later runs.
/// </summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>Environment variables set for every program, e.g. <c>{"WSL_UTF8":"1"}</c>. A step's own
    /// <c>Environment</c> is merged over these.</summary>
    [Out, Store] public global::app.type.item.dict.@this Environment { get; set; } = new();

    /// <summary>Text encoding of the program's output. Default utf-8.</summary>
    [Out, Store] public global::app.type.item.text.@this Encoding { get; set; } = "utf-8";

    /// <summary>Seconds a program may run before it is stopped. 0 = no limit (default).</summary>
    [Out, Store] public global::app.type.item.number.@this TimeoutInSec { get; set; } = 0;

    /// <summary>Most characters of output kept in the result, per stream. Default 10 MB. The
    /// <c>OnOutput</c>/<c>OnError</c> events still see every line.</summary>
    [Out, Store] public global::app.type.item.number.@this MaxOutputSize { get; set; } = 10 * 1024 * 1024;

    /// <summary>When true, the program's output is also written to plang's output channel as it
    /// arrives. Default false.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Echo { get; set; } = false;
}
