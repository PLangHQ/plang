using System.Net;

namespace PLang.Services.Channels;

public class TextReaderInputStream : IInputStream
{
    private readonly IFormatter formatter;
    private readonly TextReader reader;

    public TextReaderInputStream(TextReader reader, IFormatter formatter)
    {
        this.reader = reader;
        this.formatter = formatter;
    }

    public async Task<object?> ReceiveAsync()
    {
        return formatter.Format(await reader.ReadLineAsync());
    }

    public void SetFormatter(IFormatter formatter)
    {
        throw new NotImplementedException();
    }
}

public class TextWriterOutputStream : IOutputStream
{
    private readonly IFormatter formatter;
    private readonly TextWriter writer;

    public TextWriterOutputStream(TextWriter writer, IFormatter formatter)
    {
        this.writer = writer;
        this.formatter = formatter;
    }

    public void SetFormatter(IFormatter formatter)
    {
        this.formatter = formatter;
    }

    public async Task WriteAsync(object content)
    {
        writer.WriteLine(formatter.Format(content));
    }

    public async Task WriteChunkAsync(object chunk)
    {
        writer.Write(formatter.Format(chunk));
    }
}

public class HttpOutputStream : IOutputStream
{
    private readonly IFormatter formatter;
    private readonly HttpListenerRequest request;
    private readonly HttpListenerResponse response;

    public HttpOutputStream(HttpListenerResponse response, HttpListenerRequest request, IFormatter formatter)
    {
        this.response = response;
        this.request = request;
        this.formatter = formatter;
    }

    public void SetFormatter(IFormatter formatter)
    {
        throw new NotImplementedException();
    }


    public async Task WriteAsync(object content)
    {
        var bytes = formatter.Format(content);
        response.ContentLength64 = bytes.Length;

        await response.OutputStream.WriteAsync(bytes);
        await response.OutputStream.FlushAsync();
    }

    public async Task WriteChunkAsync(object content)
    {
        var bytes = formatter.Format(content);
        await response.OutputStream.WriteAsync(bytes);
    }
}

public class HttpChannel
{
    private HttpListenerResponse response;

    public HttpChannel(HttpListenerResponse response)
    {
        this.response = response;
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
}

public interface IInputStream
{
    Task<object?> ReceiveAsync();
    void SetFormatter(IFormatter formatter);
}

public interface IOutputStream
{
    Task WriteAsync(object content);
    Task WriteChunkAsync(object chunk);
    void SetFormatter(IFormatter formatter);
}

public interface IChannel : IDisposable
{
    IOutputStream User { get; }
    IOutputStream UserError { get; }
    IInputStream UserAsk { get; }
    IOutputStream UserNotification { get; }

    IOutputStream System { get; }
    IOutputStream SystemError { get; }
    IInputStream SystemAsk { get; }

    IOutputStream SystemMetrics { get; }
    IOutputStream SystemAudit { get; }
    IOutputStream SystemEvent { get; }
    IOutputStream SystemWarning { get; }
    IOutputStream SystemLog { get; }
    IOutputStream SystemDebug { get; }
    IOutputStream SystemTrace { get; }
}