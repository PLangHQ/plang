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

		public async Task<App.Variables.Data> Run(string[] args, CancellationToken cancellationToken = default)
		{
			// Normalize: "build" or "--build" both become the --build flag
			if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
				args = ["--build", .. args[1..]];

			var (goalFile, parameters) = CommandLineParser.Parse(args);

			var engine = new App.@this(fileSystem);
			engine.SystemDirectory = fileSystem.SystemDirectory;

			var systemVars = engine.System.Context.Variables;
			var userVars = engine.User.Context.Variables;

			// Route CLI parameters: system params (!prefix) → system Variables, user params → user Variables
			// !debug is handled separately (routed per actor)
			foreach (var param in parameters)
			{
				if (param.Key == "!debug") continue;
				if (param.Key.StartsWith("!"))
					systemVars.Set(param.Key, param.Value);
				else
					userVars.Set(param.Key, param.Value);
			}

			// Debug wiring — route to target actor, default user
			if (parameters.TryGetValue("!debug", out var debugValue) && debugValue is not false)
			{
				engine.Debug.Apply(debugValue);

				// Parse actor from JSON debug value, default "user"
				string targetActor = "user";
				if (debugValue is IDictionary<string, object?> dict &&
					dict.TryGetValue("actor", out var actorVal) && actorVal is string actorStr)
					targetActor = actorStr.ToLowerInvariant();
				else if (debugValue is Newtonsoft.Json.Linq.JObject jobj &&
					jobj.TryGetValue("actor", StringComparison.OrdinalIgnoreCase, out var actorToken))
					targetActor = actorToken.ToString().ToLowerInvariant();

				// Set on target actor(s) + system (run.pr checks %!debug% to setup events)
				systemVars.Set("!debug", debugValue);
				switch (targetActor)
				{
					case "system": break; // already on system
					case "all":
						userVars.Set("!debug", debugValue);
						break;
					default: // "user"
						userVars.Set("!debug", debugValue);
						break;
				}
			}

			// Test mode — set up test context
			if (parameters.TryGetValue("!test", out var testValue) && testValue is not false)
			{
				// Results list on system Variables — test.pr adds to it via list.add
				systemVars.Set("testResults", new List<object?>());

				// !test Data with summary that reads from testResults
				var testData = new App.Variables.Data("!test", true);
				testData.Properties["summary"] = new App.Variables.DynamicData("summary", () =>
				{
					var results = systemVars.GetValue("testResults") as List<object?>;
					if (results == null) return "No test results";
					var passed = results.Count(r => r is not App.Variables.Data d || d.Success);
					var failed = results.Count - passed;

					var sb = new System.Text.StringBuilder();
					sb.AppendLine();
					sb.AppendLine($"{passed} passed, {failed} failed out of {results.Count} tests");

					foreach (var r in results.OfType<App.Variables.Data>().Where(r => r.Success == false))
					{
						var error = r.Error;
						var step = error?.Step;
						var goalPath = step?.Goal?.Path ?? error?.Goal?.Path;

						sb.AppendLine();
						sb.AppendLine($"  \u2717 {r.Name}");
						if (step != null)
							sb.AppendLine($"    Step {step.Index}: {step.Text}");
						if (goalPath != null && step != null)
							sb.AppendLine($"      at {goalPath}:{step.LineNumber}");
						sb.AppendLine($"    {error?.Message ?? "unknown error"}");

						// Variable snapshot — show variables updated during this test
						if (r.Properties["variables"] != null)
						{
							var vars = r.Properties["variables"]?.Value as IDictionary<string, string>;
							if (vars != null && vars.Count > 0)
							{
								sb.AppendLine("    Variables:");
								foreach (var (name, value) in vars)
									sb.AppendLine($"      %{name}% = {value}");
							}
						}
					}

					return sb.ToString().TrimEnd();
				});
				engine.System.Context.Test = testData;
				systemVars.Set("!test", testData);
				if (!parameters.ContainsKey("path"))
					systemVars.Set("path", fileSystem.RootDirectory);
			}

			// Build mode — set engine flag and resolve build path
			// Supports: --build (all), --build={files:"test.goal"}, --build={files:["a.goal","b.goal"]}
			if (parameters.TryGetValue("!build", out var buildValue) && buildValue is not false)
			{
				engine.Building.IsEnabled = true;
				if (!parameters.ContainsKey("path"))
					systemVars.Set("path", fileSystem.RootDirectory);

				// Extract files filter from JSON build value → engine.Building.Files
				if (buildValue is IDictionary<string, object?> buildDict &&
					buildDict.TryGetValue("files", out var filesVal))
				{
					if (filesVal is string singleFile)
						engine.Building.Files.Add(singleFile);
					else if (filesVal is System.Collections.IEnumerable fileList)
						foreach (var f in fileList)
							if (f?.ToString() is string s) engine.Building.Files.Add(s);
				}
				else if (buildValue is Newtonsoft.Json.Linq.JObject buildJobj &&
					buildJobj.TryGetValue("files", StringComparison.OrdinalIgnoreCase, out var filesToken))
				{
					if (filesToken is Newtonsoft.Json.Linq.JArray arr)
						foreach (var item in arr) engine.Building.Files.Add(item.ToString());
					else
						engine.Building.Files.Add(filesToken.ToString());
				}
			}

			// Set the goal file for the PLang runtime (only for non-build)
			if (!engine.Building.IsEnabled)
			{
				var prPath = goalFile.Replace(".goal", ".pr", StringComparison.OrdinalIgnoreCase);
				if (!prPath.StartsWith(".build"))
					prPath = ".build/" + prPath;
				systemVars.Set("goalFile", "/" + prPath.ToLowerInvariant());
			}

			// Start the engine — system actor runs system/.build/run.pr
			return await engine.Start();
		}
	}
}
