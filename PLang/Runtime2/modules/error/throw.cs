using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.error;

/// PLang: - throw "User not found"
/// PLang: - throw "Access denied", status code 403, key "Forbidden"
[Action("throw", Cacheable = false)]
public partial class Throw : IContext
{
    [IsNotNull]
    public partial string Message { get; init; }
    [Default(500)]
    public partial int StatusCode { get; init; }
    public partial string? Key { get; init; }

    public Task<Data> Run()
    {
        return Task.FromResult(Data.FromError(
            new ServiceError(Message, Key ?? "UserError", StatusCode)));
    }
}
