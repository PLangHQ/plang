namespace app.module.http.code;

/// <summary>
/// What an exchange's exception is, as an http action answers it: a timeout (408), the transport's refusal (its
/// status), a broken stream (IOError, 500), content that isn't what it says (400). The action fails with it, and a
/// body cut short carries it in its last progress report.
/// </summary>
internal sealed class failure : global::app.error.ServiceError
{
    public failure(Exception ex) : this(ex, ex switch
    {
        TaskCanceledException => ("Timeout", 408),
        System.Net.Http.HttpRequestException hre => ("HttpError", (int)(hre.StatusCode ?? 0)),
        IOException or UnauthorizedAccessException => ("IOError", 500),
        FormatException => ("InvalidContent", 400),
        _ => ("HttpError", 500),
    }) { }

    private failure(Exception ex, (string key, int status) of) : base(ex.Message, of.key, of.status) { }
}
