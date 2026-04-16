using App.Errors;
using App.Variables;

namespace App.modules.error;

[Example("throw 'User not found'", "Message=User not found")]
[Example("throw 'Access denied', status code 403, key 'Forbidden'", "Message=Access denied, StatusCode=403, Key=Forbidden")]
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
