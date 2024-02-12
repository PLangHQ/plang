using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.SafeFileSystem;

namespace PLang.Utils
{
	public interface IErrorHelper
	{
		Task ShowFriendlyErrorMessage(Exception ex, GoalStep? step = null,
				Func<Task>? callBackForAskUser = null, Func<Exception?, Task<bool>>? errorHandler = null, Func<Task?, Task<bool>>? retryCallback = null);
	}

	public class ErrorHelper : IErrorHelper
	{
		private readonly ILogger logger;
		private readonly ILlmService aiService;
		private readonly IAskUserHandler askUserHandler;
		private readonly PLangAppContext context;
		private readonly FileAccessHandler fileAccessHandler;

		public ErrorHelper(ILogger logger, ILlmService aiService, IAskUserHandler askUserHandler, PLangAppContext context, FileAccessHandler fileAccessHandler)
		{
			this.logger = logger;
			this.aiService = aiService;
			this.askUserHandler = askUserHandler;
			this.context = context;
			this.fileAccessHandler = fileAccessHandler;
		}

		public async Task ShowFriendlyErrorMessage(Exception? ex, GoalStep? step = null,
				Func<Task>? callBackForAskUser = null, Func<Exception?, Task<bool>>? eventToRun = null,
				Func<Task?, Task<bool>>? retryCallback = null)
		{
			if (step == null && ex is RuntimeStepException rse)
			{
				step = rse.Step;
			}

			List<Exception> errors = new List<Exception>();
			Exception? loopException = ex;

			while (loopException != null)
			{
				errors.Add(loopException);
				loopException = loopException.InnerException;
			}



			context.AddOrReplace(ReservedKeywords.Exception, ex);
			if (eventToRun != null)
			{
				if (await eventToRun(ex))
				{
					return;
				}
			}

			if (context.ContainsKey(ReservedKeywords.Exception) && context[ReservedKeywords.Exception] != null)
			{
				var contextException = context[ReservedKeywords.Exception] as Exception;
				if (contextException != null && contextException.Message == "FriendlyError")
				{
					throw contextException;
				}

				//   if (context.ContainsKey(ReservedKeywords.Exception)) context.Remove(ReservedKeywords.Exception);
				string strError = "";
				foreach (var error in errors)
				{
					if (!error.Message.ToLower().StartsWith("One or more"))
					{
						strError += "\n" + error.Message;
					}
				}
				string errorInfo = "";
				if (step != null)
				{
					errorInfo += $"\nGoalName '{step.Goal.GoalName}' at {step.Goal.AbsoluteGoalPath}";
					errorInfo += $"\nStep '{step.Text}'";
					logger.LogError(errorInfo);
				}
				logger.LogError(strError);
				if (ex != null)
				{
					logger.LogWarning("--------- StackTrace ---------\n" + ex.StackTrace);
					throw new RuntimeGoalEndException("FriendlyError", step);
				}
			}

			/*
			var question = new LlmQuestion("ErrorInfo", "I am getting this error, can you give me user friendly error message and suggestion on how to fix it. Be Concise." +
					"You should respond in JSON, scheme {userFriendlyMessage:string, howToFix:string}",
					ex.ToString(), "");
			var result = await aiService.Query<ErrorHelp>(question);
			if (result != null)
			{
				logger.LogDebug(ex.ToString());
				logger.LogInformation(result.userFriendlyMessage);
				logger.LogInformation(result.howToFix);
			}*/
		}

		private string FormatStackTrace(string? stackTrace)
		{
			if (stackTrace == null || stackTrace.IndexOf("--- End of stack trace") == -1) return stackTrace;

			string firstHalf = stackTrace.Substring(0, stackTrace.IndexOf("--- End of stack trace"));
			string secondHalf = stackTrace.Replace(firstHalf, "");
			return secondHalf + "\n\n -- From top --" + firstHalf;
		}

		private async Task FileAccessRequest(FileAccessException ex, Goal goal, Func<Task>? callBackForAskUser)
		{
			var askUserFileException = new AskUserFileAccess(ex.AppName, ex.Path, ex.Message, fileAccessHandler.ValidatePathResponse);

			if (await askUserHandler.Handle(askUserFileException) && callBackForAskUser != null)
			{
				await callBackForAskUser();
			}
		}
	}
}
