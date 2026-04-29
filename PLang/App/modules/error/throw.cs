using App.Errors;
using App.Variables;

namespace App.modules.error;

[System.ComponentModel.Description("Immediately fail the step with a structured error message, status code, and optional key")]
[Action("throw", Cacheable = false)]
public partial class Throw : IContext
{
    [IsNotNull]
    public partial Data.@this<string> Message { get; init; }
    [Default(500)]
    public partial Data.@this<int> StatusCode { get; init; }
    public partial Data.@this<string>? Key { get; init; }

    public Task<Data.@this> Run()
    {
        return Task.FromResult(Error(
            new ServiceError(Message.Value!, Key?.Value ?? "UserError", StatusCode.Value)));
    }
}
