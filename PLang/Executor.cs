using App.FileSystem;
using App.FileSystem.Default;
using App.Utils;
using System.Reflection;

namespace PLang
{
	public class Executor
	{
		private readonly IPLangFileSystem fileSystem;

		public Executor(IPLangFileSystem fileSystem)
		{
			this.fileSystem = fileSystem;
		}

		public async Task<App.Data.@this> Run(string[] args, CancellationToken cancellationToken = default)
		{
			// Normalize: "build" or "--build" both become the --build flag
			if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
				args = ["--build", .. args[1..]];

			var (goalFile, parameters) = CommandLineParser.Parse(args);

			var engine = new App.@this(fileSystem);
			engine.SystemDirectory = fileSystem.SystemDirectory;

			var userVars = engine.User.Context.Variables;

			// Route CLI parameters to user Variables
			foreach (var param in parameters)
			{
				if (param.Key.StartsWith("!")) continue; // app config, not variables
				userVars.Set(param.Key, param.Value);
			}

			// Debug mode
			if (parameters.TryGetValue("!debug", out var debugValue) && debugValue is not false)
				engine.Debug.Apply(debugValue);

			// Test mode
			if (parameters.TryGetValue("!test", out var testValue) && testValue is not false)
			{
				engine.Testing.IsEnabled = true;
				if (!parameters.ContainsKey("path"))
					userVars.Set("path", fileSystem.RootDirectory);
			}

			// Build mode
			if (parameters.TryGetValue("!build", out var buildValue) && buildValue is not false)
			{
				engine.Building.IsEnabled = true;
				if (!parameters.ContainsKey("path"))
					userVars.Set("path", fileSystem.RootDirectory);

				// Extract files filter from JSON build value → engine.Building.Files
				if (buildValue is IDictionary<string, object?> buildDict &&
					buildDict.TryGetValue("files", out var filesVal))
				{
					if (filesVal is string singleFile)
						engine.Building.Files.Add(new App.FileSystem.Path(singleFile));
					else if (filesVal is System.Collections.IEnumerable fileList)
						foreach (var f in fileList)
							if (f?.ToString() is string s) engine.Building.Files.Add(new App.FileSystem.Path(s));
				}
				else if (buildValue is Newtonsoft.Json.Linq.JObject buildJobj &&
					buildJobj.TryGetValue("files", StringComparison.OrdinalIgnoreCase, out var filesToken))
				{
					if (filesToken is Newtonsoft.Json.Linq.JArray arr)
						foreach (var item in arr) engine.Building.Files.Add(new App.FileSystem.Path(item.ToString()));
					else
						engine.Building.Files.Add(new App.FileSystem.Path(filesToken.ToString()));
				}
			}

			// Set the goal file for the PLang runtime (only for normal run mode)
			if (!engine.Building.IsEnabled && !engine.Testing.IsEnabled)
			{
				var prPath = goalFile.Replace(".goal", ".pr", StringComparison.OrdinalIgnoreCase);
				if (!prPath.StartsWith(".build"))
					prPath = ".build/" + prPath;
				userVars.Set("goalFile", "/" + prPath.ToLowerInvariant());
			}

			// Start the engine — system actor runs system/.build/run.pr
			return await engine.Start();
		}
	}
}
