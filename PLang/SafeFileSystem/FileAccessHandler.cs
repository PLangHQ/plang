using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.LlmService;

namespace PLang.SafeFileSystem
{
	public class FileAccessHandler
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

		public async Task<IError?> ValidatePathResponse(string appName, string path, string? answer)
		{			
			if (string.IsNullOrWhiteSpace(answer)) return null;

			answer = answer.ToLower();
			if (answer == "n" || answer == "no") return null;
			if (settings == null) return null;

			if (answer == "y" || answer == "yes" || answer == "ok")
			{
				var expires = DateTime.UtcNow.AddSeconds(90);
				AddFileAccess(appName, path, expires);

				logger.LogDebug($"{appName} has access to {path} until {expires}");
			}
			else
			{
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
	
				var result = await llmServiceFactory.CreateHandler().Query<FileAccessResponse>(llmRequest);
				
				if (result == null || result.GiveAccess == null) return new AskUserFileAccess(appName, path, $"{appName} is trying to access {path}. Do you accept that?", this.ValidatePathResponse);
				if (result.GiveAccess.ToLower() == "no") return null;

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
			}
			return null;
		}

		private void AddFileAccess(string appName, string path, DateTime expires)
		{
			var fileAccesses = settings.GetValues<FileAccessControl>(typeof(PLangFileSystem));
			var fileAccess = fileAccesses.FirstOrDefault(a => a.appName == appName && a.path == path);
			if (fileAccess != null) {
				fileAccesses.Remove(fileAccess);
			}

			fileAccesses.Add(new FileAccessControl(appName, path, expires));
			settings.SetList(typeof(PLangFileSystem), fileAccesses);
			fileSystem.SetFileAccess(fileAccesses);
		}
	}
}
