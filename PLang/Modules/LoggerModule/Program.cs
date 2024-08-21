using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace PLang.Modules.LoggerModule
{
	public class Program : BaseProgram
	{
		private readonly ILogger logger;

		public Program(ILogger logger)
		{
			this.logger = logger;
		}

		[Description("loggerLevel can be trace, debug, information, warning, error. args can be null if not defined")]
		public async Task Log(string message, string loggerLevel = "information", params object[]? args)
		{
			Enum.TryParse(loggerLevel, true, out LogLevel logLevelStartup);
			logger.Log(logLevelStartup, message, args);
		}
	}
}
