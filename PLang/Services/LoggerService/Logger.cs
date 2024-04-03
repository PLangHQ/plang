using Microsoft.Extensions.Logging;

namespace PLang.Services.LoggerService
{

	public class Logger<T> : ILogger<T>
	{
		public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			LogLevel? logLevelByUser = LogLevel.Warning;

			if (AppContext.TryGetSwitch("Builder", out bool isEnabled) && isEnabled)
			{
				logLevelByUser = LogLevel.Information;
			}
			if (AppContext.GetData("StepLogLevelByUser") != null)
			{
				logLevelByUser = (LogLevel?)AppContext.GetData("StepLogLevelByUser");
			}
			if (AppContext.GetData("GoalLogLevelByUser") != null)
			{
				logLevelByUser = (LogLevel?)AppContext.GetData("GoalLogLevelByUser");

			}

			string? loggerLevel = AppContext.GetData("--logger") as string;
			if (loggerLevel != null)
			{
				if (Enum.TryParse(loggerLevel, true, out LogLevel logLevelStartup))
				{
					if (logLevelByUser > logLevelStartup)
					{
						logLevelByUser = logLevelStartup;
					}
				}
				else
				{
					AppContext.SetData("--logger", null);
					Console.WriteLine($"Could not set logger level to {loggerLevel}. You can set: Debug, Information, Warning, Error, Trace");
				}

			}

			if (logLevel < logLevelByUser)
			{
				return;
			}

			if (logLevel == LogLevel.Debug)
			{
				Console.ForegroundColor = ConsoleColor.Cyan;
			}
			else if (logLevel == LogLevel.Information)
			{
				Console.ForegroundColor = ConsoleColor.Blue;
			}
			else if (logLevel == LogLevel.Warning)
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.BackgroundColor = ConsoleColor.Black;
			}
			else if (logLevel == LogLevel.Error)
			{
				Console.ForegroundColor = ConsoleColor.Red;
			}
			else if (logLevel == LogLevel.Trace)
			{
				Console.ForegroundColor = ConsoleColor.Magenta;
			}

			Console.WriteLine($"{state} - ({logLevel}) - {exception}");
			Console.ResetColor();

		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return true;
		}

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		{
			return null;
		}
	}
}
