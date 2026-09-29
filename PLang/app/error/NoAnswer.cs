namespace app.error;

/// <summary>
/// An ask no one can answer — a closed, non-interactive input (the stream's end). Its key is <c>ChannelEof</c>.
/// </summary>
public class NoAnswer : ServiceError
{
    public NoAnswer(string message) : base(message, "ChannelEof", 400) { }

    /// <summary>Nobody could answer the ask.</summary>
    public override bool Unanswered => true;
}
