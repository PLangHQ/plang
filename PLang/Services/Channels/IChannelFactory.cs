using PLang.Errors;
using PLang.Modules.OutputModule;

namespace PLang.Services.Channels;

public interface IChannel
{
    public MessageType MessageType
    {
        get;
        set;
    }
    Task<string?> Ask(AskProperties askProperties);
    Task<IError?> Write(WriteProperties writeProperties);
}
