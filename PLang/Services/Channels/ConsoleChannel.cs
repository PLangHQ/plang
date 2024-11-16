namespace PLang.Services.Channels;

public class ConsoleChannel : IChannel
{
    public ConsoleChannel()
    {
        User = new TextWriterOutputStream(Console.Out);
        UserError = new TextWriterOutputStream(Console.Out);
        UserAsk = new TextReaderInputStream(Console.In);
    }

    public IOutputStream User { get; set; }

    public IOutputStream UserError { get; set; }

    public IInputStream UserAsk { get; set; }

    public IOutputStream UserNotification { get; set; }

    public IOutputStream System { get; set; }

    public IOutputStream SystemError { get; set; }

    public IInputStream SystemAsk { get; set; }

    public IOutputStream SystemMetrics { get; set; }

    public IOutputStream SystemAudit { get; set; }

    public IOutputStream SystemEvent { get; set; }

    public IOutputStream SystemWarning { get; set; }

    public IOutputStream SystemLog { get; set; }

    public IOutputStream SystemDebug { get; set; }

    public IOutputStream SystemTrace { get; set; }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}