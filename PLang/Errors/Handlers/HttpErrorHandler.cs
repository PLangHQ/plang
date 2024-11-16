using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Exceptions.AskUser;

namespace PLang.Errors.Handlers;

public class HttpErrorHandler : BaseErrorHandler, IErrorHandler
{
    private readonly HttpListenerContext httpListenerContext;
    private readonly ILogger logger;

    public HttpErrorHandler(HttpListenerContext httpListenerContext, IAskUserHandlerFactory askUserHandlerFactory,
        ILogger logger) : base(askUserHandlerFactory)
    {
        this.httpListenerContext = httpListenerContext;
        this.logger = logger;
    }

    public async Task<(bool, IError?)> Handle(IError error)
    {
        return await base.Handle(error);
    }

    public async Task ShowError(IError error, GoalStep? step)
    {
        try
        {
            var resp = httpListenerContext.Response;
            resp.StatusCode = error.StatusCode;
            resp.StatusDescription = error.Key;

            using (var writer = new StreamWriter(resp.OutputStream, resp.ContentEncoding ?? Encoding.UTF8))
            {
                var str = error.ToFormat("json").ToString();
                await writer.WriteAsync(str);
                await writer.FlushAsync();
            }
        }
        catch (ObjectDisposedException)
        {
            logger.LogWarning($"Object disposed when writing error to response:\n{error}");
        }
    }
}