using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.LlmService;
using PLang.Utils;

namespace PLang.SafeFileSystem
{
	public interface IFileAccessHandler
	{
		Task<(bool, IError?)> ValidatePathResponse(string appName, string path, string? answer, string processId);
		void GiveAccess(string appName, string path);
	}
	public class FileAccessHandler : IFileAccessHandler
	{
		private readonly ISettings settings;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly ILogger logger;
		private readonly IPLangFileSystem fileSystem;

		public FileAccessHandler(ISettings settings, ILlmServiceFactory llmServiceFactory, ILogger logger, IPLangFileSystem fileSystem)
		{
			this.settings = settings;
			this.llmServiceFactory = llmServiceFactory;
			this.logger = logger;
			this.fileSystem = fileSystem;
		}

		public record FileAccessResponse(string GiveAccess, DateTime? Expires);

		//very basic access control, could add Access type(read, write, del), status such as blocked
		//appName is weak validation, need to find new way

		// using a proof of access should probably be the solution
		// when getting access, sign the request with root
		// the signature is then validated by root before giving access next time 

		public async Task<(bool, IError?)> ValidatePathResponse(string appName, string path, string? answer, string processId)
		{
			if (string.IsNullOrWhiteSpace(answer)) return (false, null);

			answer = answer.ToLower();
			if (answer == "n" || answer == "no") return (true, null);
			if (settings == null) return (false, new Error("Settings is not loaded", StatusCode: 500));

			if (answer == "y" || answer == "yes")
			{
				AddFileAccess(appName, path, null, processId);

				logger.LogDebug($"{appName} has access to {path} on processId {processId}");
				return (true, null);
			}

			if (answer == "a" || answer == "always")
			{
				var expires = DateTime.UtcNow.AddYears(100);
				AddFileAccess(appName, path, expires);
				 
				logger.LogDebug($"{appName} has access to {path} until {expires}");
				return (true, null);
			}
			 

			var dateTimeStr = DateTimeOffset.UtcNow.ToString("G");

			var promptMessage = new List<LlmMessage>();
			promptMessage.Add(new LlmMessage("system", @$"The user response should answer the question: Should give access to a file path

Determine the answer from the user that fits the json scheme. 
If you cannot determine, GiveAccess should be null
if user likes to give never ending expire time, set Expires to 99 years into the future
Expires should be in the format yyyy-MM-ddTHH:mm:ss

current time is {dateTimeStr}
GiveAccess : yes|no|null"));
			promptMessage.Add(new LlmMessage("user", answer));

			var llmRequest = new LlmRequest("FileAccess", promptMessage);

			(var result, var requestError) = await llmServiceFactory.CreateHandler().Query<FileAccessResponse>(llmRequest);
			if (requestError != null) return (false, requestError);

			if (result == null || result.GiveAccess == null) return (false, new FileAccessRequestError(appName, path, $"{appName} is trying to access {path}. Do you accept that?"));
			if (result.GiveAccess.ToLower() == "no") return (true, null);

			if (result.GiveAccess.ToLower() == "yes")
			{
				var expires = result.Expires ?? DateTime.UtcNow.AddSeconds(30);

				if (dateTimeStr == expires.ToString("G"))
				{
					expires = DateTime.UtcNow.AddSeconds(30);
				}

				AddFileAccess(appName, path, expires);
				logger.LogDebug($"{appName} has access to {path} until {expires}");
			}

			return (true, null);
		}

		private void AddFileAccess(string appName, string path, DateTime? expires = null, string? processId = null)
		{
			path = path.AdjustPathToOs();
			var fileAccesses = settings.GetValues<FileAccessControl>(typeof(PLangFileSystem));
			var fileAccess = fileAccesses.FirstOrDefault(a => a.appName == appName && a.path == path);
			if (fileAccess != null)
			{
				fileAccesses.Remove(fileAccess);
			}

			fileAccesses.Add(new FileAccessControl(appName, path, expires, processId));
			settings.SetList(typeof(PLangFileSystem), fileAccesses);
			fileSystem.SetFileAccess(fileAccesses);
		}

		public void GiveAccess(string appName, string path)
		{
			path = path.AdjustPathToOs();

			fileSystem.AddFileAccess(new FileAccessControl(appName, path, ProcessId: fileSystem.Id));
		}
	}
}
