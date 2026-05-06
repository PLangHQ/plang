using App.Callback;

namespace App.Channels.Channel;

/// <summary>
/// Payload passed to channel-event handlers (BeforeWrite/AfterWrite/BeforeRead/
/// AfterRead/OnAsk). Exposes the firing channel, the in-flight Data envelope,
/// and (for OnAsk) the AskCallback being prepared/answered.
/// </summary>
public sealed class EventContext
{
    public required @this Channel { get; init; }
    public required Data.@this Data { get; init; }
    public AskCallback? Ask { get; init; }
}
