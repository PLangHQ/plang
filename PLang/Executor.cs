using LightInject;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Utils;
using System.Reflection;

namespace PLang
{
	public class Executor
	{
		private readonly IPLangFileSystem fileSystem;

		public Executor(IServiceContainer container)
		{
			this.fileSystem = container.GetInstance<IPLangFileSystem>();
		}

		public enum ExecuteType
		{
			Runtime = 0,
			Builder = 1
		}

		public async Task<(object? Variables, IError? Error)> Execute(string[] args, ExecuteType executeType, CancellationToken cancellationToken = default)
		{
			if (args.FirstOrDefault(p => p == "--version") != null)
			{
				Console.WriteLine("plang version: " + Assembly.GetAssembly(this.GetType())!.GetName().Version);
				return (null, null);
			}

			var result = await Run(args, cancellationToken);
			if (!result.Success && result.Error != null)
				return (result.Value, new Error(result.Error.Format()));
			return (result.Value, null);
		}

		public async Task<Runtime2.Engine.Memory.Data> Run(string[] args, CancellationToken cancellationToken = default)
		{
			// Normalize: "build" or "--build" both become the --build flag
			if (args.Length > 0 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase))
				args = ["--build", .. args[1..]];

			var (goalFile, parameters) = CommandLineParser.Parse(args);

			var engine = new Runtime2.Engine.@this(fileSystem);
			engine.SystemDirectory = fileSystem.SystemDirectory;

			var systemMs = engine.System.Context.MemoryStack;
			var userMs = engine.User.Context.MemoryStack;

			// Route CLI parameters: system params (!prefix) → system MemoryStack, user params → user MemoryStack
			// !debug is handled separately (routed per actor)
			foreach (var param in parameters)
			{
				if (param.Key == "!debug") continue;
				if (param.Key.StartsWith("!"))
					systemMs.Set(param.Key, param.Value);
				else
					userMs.Set(param.Key, param.Value);
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
				systemMs.Set("!debug", debugValue);
				switch (targetActor)
				{
					case "system": break; // already on system
					case "all":
						userMs.Set("!debug", debugValue);
						break;
					default: // "user"
						userMs.Set("!debug", debugValue);
						break;
				}
			}

			// Test mode — set up test context
			if (parameters.TryGetValue("!test", out var testValue) && testValue is not false)
			{
				// Results list on system MemoryStack — test.pr adds to it via list.add
				systemMs.Set("testResults", new List<object?>());

				// !test Data with summary that reads from testResults
				var testData = new Runtime2.Engine.Memory.Data("!test", true);
				testData.Properties["summary"] = new Runtime2.Engine.Memory.DynamicData("summary", () =>
				{
					var results = systemMs.GetValue("testResults") as List<object?>;
					if (results == null) return "No test results";
					var passed = results.Count(r => r is Runtime2.Engine.Memory.Data d && d.Success);
					var failed = results.Count - passed;

					var sb = new System.Text.StringBuilder();
					sb.AppendLine();
					sb.AppendLine($"{passed} passed, {failed} failed out of {results.Count} tests");

					foreach (var r in results.OfType<Runtime2.Engine.Memory.Data>().Where(r => !r.Success))
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
				systemMs.Set("!test", testData);
				if (!parameters.ContainsKey("path"))
					systemMs.Set("path", fileSystem.RootDirectory);
			}

			// Build mode — set engine flag and resolve build path
			if (parameters.TryGetValue("!build", out var buildValue) && buildValue is not false)
			{
				engine.Building.IsEnabled = true;
				if (!parameters.ContainsKey("path"))
					systemMs.Set("path", fileSystem.RootDirectory);
			}

			// Set the goal file for the PLang runtime (only for non-build)
			if (!engine.Building.IsEnabled)
			{
				var prPath = goalFile.Replace(".goal", ".pr", StringComparison.OrdinalIgnoreCase);
				if (!prPath.StartsWith(".build"))
					prPath = ".build/" + prPath;
				systemMs.Set("goalFile", "/" + prPath.ToLowerInvariant());
			}

			// Start the engine — system actor runs system/.build/run.pr
			return await engine.Start();
		}
	}
}
