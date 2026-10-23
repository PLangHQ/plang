namespace app.channel.type.console;

/// <summary>
/// The console — the process's standard streams (output, error, input): a stream channel whose format is the channel
/// setting's (<c>%!app.channel.setting.format%</c>), not a MIME of its own. Text by default, for a person at a
/// terminal; plang's own for a program that runs this plang (<c>--channel.setting.format=plang</c>); bytes for a
/// value's own bytes, with no newline after them.
/// </summary>
public sealed class @this : global::app.channel.type.stream.@this
{
    /// <summary>A console channel over <paramref name="stream"/>, the process's — never owned: the process closes it.
    /// What it writes is framed: each message ends with a newline when its format is written in lines.</summary>
    public @this(string name, global::System.IO.Stream stream, ChannelDirection direction)
        : base(name, stream, direction, ownsStream: false)
    {
        Framed = direction != ChannelDirection.Input;
    }

    /// <summary>The channel setting's format, as <paramref name="context"/>'s settings have it now.</summary>
    protected override async System.Threading.Tasks.ValueTask<global::app.type.kind.@this> Format(global::app.actor.context.@this context)
        => (await context.Setting.Get<global::app.channel.setting.@this>()).Format.Kind;
}
