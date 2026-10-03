namespace app.module.http.code;

/// <summary>
/// An upload's content as it is sent: the inner content read through a <see cref="body"/>, so its progress is reported
/// as it goes (<c>sent</c>) and once more when it is all sent. Its headers are the inner content's.
/// </summary>
internal sealed class content : System.Net.Http.HttpContent
{
    private readonly System.Net.Http.HttpContent _inner;
    private readonly System.Func<global::app.data.@this, Task> _report;
    private readonly actor.context.@this _context;

    public content(System.Net.Http.HttpContent inner, System.Func<global::app.data.@this, Task> report, actor.context.@this context)
    {
        _inner = inner;
        _report = report;
        _context = context;
        foreach (var header in inner.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    protected override async Task SerializeToStreamAsync(System.IO.Stream stream, System.Net.TransportContext? transport)
    {
        await using var source = await _inner.ReadAsStreamAsync();
        await using var body = new body(source, _inner.Headers.ContentLength, max: null, sent: true, _report, tee: null, _context);
        await body.CopyToAsync(stream);
        await body.Done();
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _inner.Headers.ContentLength ?? -1;
        return length >= 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}
