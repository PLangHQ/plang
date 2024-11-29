using PLang.Errors;
using PLang.Modules.OutputModule;
using PLang.Services.Channels.Formatters;

namespace PLang.Services.Channels;

public class ConsoleChannel : IChannel
{
    private readonly IMessageTypeFormatter formatter;

    public ConsoleChannel(IMessageTypeFormatter formatter)
    {
        this.formatter = formatter;
    }

    public MessageType MessageType { get; set; }

    public async Task<string?> Ask(AskProperties askProperties)
    {
        if (askProperties.HasError)
        {
            Console.WriteLine(askProperties.ErrorMessage);
        }

        Console.WriteLine(askProperties.Question);
        return await Console.In.ReadLineAsync();
    }

    public Task<IError?> Write(WriteProperties writeProperties)
    {
        var text = formatter.Format(writeProperties.Data, MessageType, writeProperties.StatusCode);

        Console.Write(text);
        return Task.FromResult<IError?>(null);
    }


    public static Dictionary<MessageType, IChannel> GetDefaultChannels()
    {
        var formatter = new ConsoleFormatter();
        var consoleChannel = new ConsoleChannel(formatter);
        var channels = new Dictionary<MessageType, IChannel>
        {
            { MessageType.UserOutput, consoleChannel },
            { MessageType.UserAsk, consoleChannel },
            { MessageType.UserError, consoleChannel },
            { MessageType.UserNotification, consoleChannel },
            { MessageType.SystemOutput, consoleChannel },
            { MessageType.SystemError, consoleChannel },
            { MessageType.SystemAsk, consoleChannel },
            { MessageType.SystemNotification, consoleChannel },
            { MessageType.SystemMetrics, consoleChannel },
            { MessageType.SystemAudit, consoleChannel },
            { MessageType.SystemEvent, consoleChannel },
            { MessageType.SystemDebug, consoleChannel },
            { MessageType.SystemWarning, consoleChannel },
            { MessageType.SystemLog, consoleChannel },
            { MessageType.SystemTrace, consoleChannel }
        };
        return channels;
    }
}