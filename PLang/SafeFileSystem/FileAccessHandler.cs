using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;

namespace PLang.SafeFileSystem
{
	public class FileAccessHandler
	{
		private readonly ISettings settings;
		private readonly ILlmService llmService;
		private readonly ILogger logger;
		private readonly IPLangFileSystem fileSystem;

		public FileAccessHandler(ISettings settings, ILlmService llmService, ILogger logger, IPLangFileSystem fileSystem)
		{
			this.settings = settings;
			this.llmService = llmService;
			this.logger = logger;
			this.fileSystem = fileSystem;
		}

		public record FileAccessResponse(string GiveAccess, DateTime? Expires);

		//very basic access control, could add Access type(read, write, del), status such as blocked
		//appName is weak validation, need to find new way

		// using a proof of access should probably be the solution
		// when getting access, sign the request with root
		// the signature is then validated by root before giving access next time

		public async Task ValidatePathResponse(string appName, string path, string answer)
		{
			answer = answer.ToLower();
			if (answer == "n" || answer == "no") return;
			if (settings == null) return;

			if (answer == "y" || answer == "yes" || answer == "ok")
			{
				var expires = DateTime.UtcNow.AddSeconds(10);
				AddFileAccess(appName, path, expires);

				logger.LogDebug($"{appName} has access to {path} until {expires}");
			}
			else
			{
				var dateTimeStr = DateTimeOffset.UtcNow.ToString("G");
				var llmQuestion = new LlmQuestion("FileAccess",
@$"The user response should answer the question: Should give access to a file path

Determine the answer from the user that fits the json scheme. 
If you cannot determine, GiveAccess should be null
if user likes to give never ending expire time, set Expires to 99 years into the future
Expires should be in the format yyyy-MM-ddTHH:mm:ss

current time is {dateTimeStr}

you must return in json scheme
{{ GiveAccess : yes|no|null, Expires: datetime }}", "user response: " + answer, "");
				var result = await llmService.Query<FileAccessResponse>(llmQuestion);
				
				if (result == null || result.GiveAccess == null) throw new FileAccessException(appName, path, $"{appName} is trying to access {path}. Do you accept that?");
				if (result.GiveAccess.ToLower() == "no") return;

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
