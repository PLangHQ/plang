using app.errors;
using app.variables;

namespace app.modules.error;

[System.ComponentModel.Description("Immediately fail the step with a structured error message, status code, and optional key")]
[Action("throw", Cacheable = false)]
public partial class Throw : IContext
{
    [IsNotNull]
    public partial data.@this<string> Message { get; init; }
    [Default(500)]
    public partial data.@this<int> StatusCode { get; init; }
    public partial data.@this<string>? Key { get; init; }

    public Task<data.@this> Run()
    {
        return Task.FromResult(Error(
            new ServiceError(Message.Value!, Key?.Value ?? "UserError", StatusCode.Value)));
    }
}
