using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace PLang.Modules.LoggerModule;

public class Program : BaseProgram
{
    private readonly ILogger logger;

    public Program(ILogger logger)
    {
        this.logger = logger;
    }

    [Description("loggerLevel can be trace, debug, information, warning, error. args can be null if not defined")]
    public async Task Log(string message, string loggerLevel = "information", object[]? args = null)
    {
        Enum.TryParse(loggerLevel, true, out LogLevel logLevelStartup);
        if (args != null)
            logger.Log(logLevelStartup, message, args);
        else
            logger.Log(logLevelStartup, message);
    }
}