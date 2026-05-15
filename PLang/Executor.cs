using app.FileSystem;
using app.FileSystem.Default;
using app.Utils;
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

		public async Task<app.data.@this> Run(string[] args, CancellationToken cancellationToken = default)
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
		internal (app.@this? Engine, app.data.@this? Error) Configure(string[] args)
		{
			// Normalize: "build" or "--builder" both become the --builder flag.
			// Legacy `plang build` form preserved as ergonomics; --builder is canonical.
			if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
				args = ["--builder", .. args[1..]];

			var (goalFile, parameters) = CommandLineParser.Parse(args);

			var engine = new app.@this(fileSystem);
			engine.OsDirectory = fileSystem.OsDirectory;

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

			// Tester mode (--tester or legacy --test)
			if ((parameters.TryGetValue("!tester", out var testValue) && testValue is not false) ||
			    (parameters.TryGetValue("!test", out testValue) && testValue is not false))
			{
				engine.Tester.IsEnabled = true;
				if (!parameters.ContainsKey("path"))
					userVars.Set("path", fileSystem.RootDirectory);

				if (testValue is IDictionary<string, object?> testDict)
				{
					var applyResult = engine.Tester.Apply(testDict);
					if (!applyResult.Success) return (null, applyResult);
				}
			}

			// App settings (--app={"create":true})
			if (parameters.TryGetValue("!app", out var appValue) && appValue is IDictionary<string, object?> appDict)
				global::app.Types.@this.Populate(engine, appDict);

			// Builder mode (--builder or legacy --build)
			if ((parameters.TryGetValue("!builder", out var buildValue) && buildValue is not false) ||
			    (parameters.TryGetValue("!build", out buildValue) && buildValue is not false))
			{
				engine.Builder.IsEnabled = true;
				if (!parameters.ContainsKey("path"))
					userVars.Set("path", fileSystem.RootDirectory);

				if (buildValue is IDictionary<string, object?> buildDict)
					global::app.Types.@this.Populate(engine.Builder, buildDict);

				// Sync cache flag to %!build.cache% for Build.goal
				userVars.Set("!build.cache", engine.Builder.Cache);
			}

			// Set the goal file on system context — Start() reads it
			// Tester mode routes to system test runner instead of Start.goal
			if (engine.Tester.IsEnabled && goalFile == "Start.goal")
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
