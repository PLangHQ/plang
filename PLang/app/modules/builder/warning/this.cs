namespace app.modules.builder.warning;

/// <summary>
/// Build-time warning payload. Written by IClass.Build() implementations to the
/// "builder" channel; consumed by the builder, --strict mode, and trace
/// rendering. In-band errors stay on Data (caller short-circuits, must be in
/// return path); out-of-band advisory warnings travel through this channel —
/// no Data shape change required.
///
/// <para>
/// The writing handler puts <c>this</c> in <see cref="Action"/> so the consumer
/// has source attribution without channel-side caller-tagging magic.
/// </para>
/// </summary>
public sealed record @this(global::app.modules.IClass Action, string Message);
