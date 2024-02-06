using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.SafeFileSystem;
using System.IO.Compression;

namespace PLang.Services.AppsRepository
{
	public interface IPLangAppsRepository
	{
		void InstallApp(string appName);
	}
	public class PLangAppsRepository(PLangFileSystem fileSystem, IHttpClientFactory httpClient, PrParser prParser) : IPLangAppsRepository
	{
		public void InstallApp(string appName)
		{
			if (fileSystem.Directory.Exists(Path.Join("apps", appName))) return;

			string zipPath = Path.Join(fileSystem.RootDirectory, "apps", appName, appName + ".zip");
			using (var client = httpClient.CreateClient())
			{
				try
				{
					client.DefaultRequestHeaders.UserAgent.ParseAdd("plang v0.1");
					using (var s = client.GetStreamAsync($"https://raw.githubusercontent.com/PLangHQ/apps/main/{appName}/{appName}.zip"))
					{
						fileSystem.Directory.CreateDirectory(Path.Join("apps", appName));
						using (var fs = new FileStream(zipPath, FileMode.OpenOrCreate))
						{
							s.Result.CopyTo(fs);
						}
					}

					if (fileSystem.File.Exists(zipPath))
					{
						ZipFile.ExtractToDirectory(zipPath, Path.Join(fileSystem.RootDirectory, "apps", appName), true);
						prParser.ForceLoadAllGoals();
					}

				}
				catch (Exception ex)
				{
					throw new RuntimeException($"Could not find app {appName} at https://github.com/PLangHQ/apps/. You must put {appName} folder into the apps folder before calling it.");
				}
			}



		}
	}
}
