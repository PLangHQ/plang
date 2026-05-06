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
			var (engine, configError) = Configure(args);
			if (configError != null) return configError;
			return await engine!.Start();
		}

		/// <summary>
		/// Parses argv and prepares an App engine for execution: wires CLI parameters
		/// to user variables, applies --test / --debug / --build / --app config, and
		/// sets the goalFile variable on System.Context that Start() reads.
		/// Returns (engine, null) on success, (null, errorData) if --test= config is invalid.
		/// Separated from Run() so tests can observe configuration without executing Start().
		/// </summary>
		internal (App.@this? Engine, App.Data.@this? Error) Configure(string[] args)
		{
			// Normalize: "build" or "--build" both become the --build flag
			if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
				args = ["--build", .. args[1..]];

			var (goalFile, parameters) = CommandLineParser.Parse(args);

			var engine = new App.@this(fileSystem);
			engine.OsDirectory = fileSystem.OsDirectory;

			// Stage 6: entry-point wires console standard streams. App ctor no longer
			// auto-opens them; the invariant check on Run() ensures they're present.
			App.@this.WireDefaultConsoleChannels(engine.System);
			App.@this.WireDefaultConsoleChannels(engine.User);

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

				if (testValue is IDictionary<string, object?> testDict)
				{
					var applyResult = engine.Testing.Apply(testDict);
					if (!applyResult.Success) return (null, applyResult);
				}
			}

			// App settings (--app={"create":true})
			if (parameters.TryGetValue("!app", out var appValue) && appValue is IDictionary<string, object?> appDict)
				TypeMapping.Populate(engine, appDict);

			// Build mode
			if (parameters.TryGetValue("!build", out var buildValue) && buildValue is not false)
			{
				engine.Build.IsEnabled = true;
				if (!parameters.ContainsKey("path"))
					userVars.Set("path", fileSystem.RootDirectory);

				if (buildValue is IDictionary<string, object?> buildDict)
					TypeMapping.Populate(engine.Build, buildDict);

				// Sync cache flag to %!build.cache% for Build.goal
				userVars.Set("!build.cache", engine.Build.Cache);
			}

			// Set the goal file on system context — Start() reads it
			// Test mode routes to system test runner instead of Start.goal
			if (engine.Testing.IsEnabled && goalFile == "Start.goal")
			{
				engine.System.Context.Variables.Set("goalFile", "/system/.build/test.pr");
				return (engine, null);
			}

			var prPath = goalFile.Replace(".goal", ".pr", StringComparison.OrdinalIgnoreCase);
			if (!prPath.StartsWith(".build"))
				prPath = ".build/" + prPath;
			engine.System.Context.Variables.Set("goalFile", "/" + prPath.ToLowerInvariant());

			return (engine, null);
		}
	}
}
