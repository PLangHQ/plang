using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PLang.Modules.WebserverModule
{
	public record EffectSummary(
		SortedSet<string> DbWrites, SortedSet<string> Files, SortedSet<string> External,
		SortedSet<string> Costs, SortedSet<string> Opaque, SortedSet<string> Unknown)
	{
		public bool Writes => DbWrites.Count > 0 || Files.Count > 0;

		public static EffectSummary Empty() => new(new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal),
			new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal));

		public string Tag()
		{
			var tags = new List<string>();
			if (Writes) tags.Add("changes data");
			if (External.Count > 0) tags.Add("external");
			if (Costs.Count > 0) tags.Add("costs");
			if (Opaque.Count > 0) tags.Add("opaque");
			if (Unknown.Count > 0) tags.Add("unknown");
			return tags.Count == 0 ? "read" : string.Join("; ", tags);
		}
	}

	public record StepInfo(string Module, string? Function, string? Sql, string? SqlFile, string? Table, string? DataSource, List<string> SubGoals);

	// Computes what a goal changes by reading the structured step files the builder wrote (.pr),
	// following calls into sub goals. What a module method does is declared on the method with [Effect],
	// the only analysis done here is of SQL text, for the methods marked FromSql. Nothing reads goal text.
	public class GoalEffects
	{
		private const int MaxDepth = 8;

		private static readonly Regex WriteSql = new(
			@"\b(?<kind>insert\s+(?:or\s+\w+\s+)?into|replace\s+into|update\s+(?:or\s+\w+\s+)?(?=\[?""?`?\w+\]?""?`?\s+set\b)|delete\s+from|create\s+table(?:\s+if\s+not\s+exists)?|alter\s+table|drop\s+table(?:\s+if\s+exists)?)\s*\[?""?`?(?<table>\w+)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static readonly ConcurrentDictionary<string, (DateTime Written, StepInfo Info)> StepCache = new();
		private static readonly ConcurrentDictionary<string, Effects?> DeclaredCache = new();

		private readonly Func<string, Goal?> getGoalByPrPath;
		private readonly Func<string, string, string?> readSqlFile;

		public GoalEffects(Func<string, Goal?> getGoalByPrPath, Func<string, string, string?> readSqlFile)
		{
			this.getGoalByPrPath = getGoalByPrPath;
			this.readSqlFile = readSqlFile;
		}

		public EffectSummary Analyze(Goal goal)
		{
			var summary = EffectSummary.Empty();
			Walk(goal, summary, new HashSet<string>(), 0);
			return summary;
		}

		private void Walk(Goal goal, EffectSummary summary, HashSet<string> visited, int depth)
		{
			if (depth > MaxDepth || !visited.Add(goal.RelativePrPath ?? goal.GoalName)) return;

			foreach (var step in goal.GoalSteps)
			{
				var info = ReadStep(step.AbsolutePrFilePath);
				if (info == null) continue;

				Apply(info, goal, summary);

				foreach (var subGoalPath in info.SubGoals)
				{
					var subGoal = getGoalByPrPath(subGoalPath);
					if (subGoal != null) Walk(subGoal, summary, visited, depth + 1);
				}
			}
		}

		private void Apply(StepInfo info, Goal goal, EffectSummary summary)
		{
			var name = info.Module.Substring(info.Module.LastIndexOf('.') + 1).Replace("Module", "") + (info.Function == null ? "" : "." + info.Function);
			var declared = Declared(info.Module, info.Function);
			if (declared == null)
			{
				summary.Unknown.Add(name);
				return;
			}

			var effects = declared.Value;
			if (effects.HasFlag(Effects.WritesData) || effects.HasFlag(Effects.FromSql))
			{
				AddDbWrites(info, name, goal, summary, effects.HasFlag(Effects.FromSql));
			}
			if (effects.HasFlag(Effects.WritesFiles)) summary.Files.Add(name);
			if (effects.HasFlag(Effects.External)) summary.External.Add(name);
			if (effects.HasFlag(Effects.Costs)) summary.Costs.Add(name);
			if (effects.HasFlag(Effects.Opaque)) summary.Opaque.Add(name);
		}

		private void AddDbWrites(StepInfo info, string name, Goal goal, EffectSummary summary, bool fromSql)
		{
			var sql = info.Sql;
			if (sql == null && info.SqlFile != null) sql = readSqlFile(info.SqlFile, goal.AbsoluteGoalFolderPath);

			var found = false;
			if (!string.IsNullOrEmpty(sql))
			{
				foreach (Match match in WriteSql.Matches(sql))
				{
					var kind = match.Groups["kind"].Value.Trim().ToLowerInvariant().Split(' ')[0];
					summary.DbWrites.Add($"{info.DataSource ?? "data"}.{match.Groups["table"].Value} ({kind})");
					found = true;
				}
			}

			if (found) return;
			if (fromSql)
			{
				if (string.IsNullOrEmpty(sql)) summary.Unknown.Add(name + " (sql not known at build)");
				return;
			}

			summary.DbWrites.Add($"{info.DataSource ?? "data"}.{info.Table ?? "?"} ({name})");
		}

		private static Effects? Declared(string module, string? function)
		{
			var key = module + "|" + function;
			if (DeclaredCache.TryGetValue(key, out var cached)) return cached;

			Effects? result = null;
			var type = typeof(EffectAttribute).Assembly.GetType(module + ".Program");
			if (type != null)
			{
				var methodLevel = function == null ? new List<Effects>() : type
					.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					.Where(m => m.Name == function)
					.Select(m => m.GetCustomAttribute<EffectAttribute>()?.Effects)
					.Where(e => e != null)
					.Select(e => e!.Value)
					.ToList();

				result = methodLevel.Count > 0
					? methodLevel.Aggregate(Effects.None, (all, e) => all | e)
					: type.GetCustomAttribute<EffectAttribute>()?.Effects;
			}

			DeclaredCache[key] = result;
			return result;
		}

		private StepInfo? ReadStep(string? path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

			var written = File.GetLastWriteTimeUtc(path);
			if (StepCache.TryGetValue(path, out var cached) && cached.Written == written) return cached.Info;

			var info = Parse(JObject.Parse(File.ReadAllText(path)));
			if (info != null) StepCache[path] = (written, info);
			return info;
		}

		private StepInfo? Parse(JObject pr)
		{
			var moduleType = pr["ModuleType"]?.ToString();
			if (moduleType == null) return null;

			var function = pr["Function"] as JObject;
			var parameters = function?["Parameters"] as JArray ?? new JArray();

			string? Param(string name) => (parameters
				.OfType<JObject>()
				.FirstOrDefault(p => string.Equals(p["Name"]?.ToString(), name, StringComparison.OrdinalIgnoreCase)))?["Value"]?.ToString();

			var validSql = (pr["Properties"] as JObject)?["IsValidSql"];
			var firstValidSql = validSql is JArray validSqlList ? validSqlList.FirstOrDefault() : validSql;
			var dataSource = (firstValidSql as JObject)?["DataSourceName"]?.ToString()
				?? Param("dataSourceName")
				?? (parameters.OfType<JObject>().FirstOrDefault(p => p["Name"]?.ToString() == "dataSourceNames")?["Value"] as JArray)?.FirstOrDefault()?.ToString();

			var subGoals = new List<string>();
			foreach (var parameter in parameters.OfType<JObject>())
			{
				CollectGoalPaths(parameter["Value"], subGoals);
			}

			return new StepInfo(moduleType, function?["Name"]?.ToString(), Param("sql"), Param("fileName"),
				Param("tableName") ?? Param("table"), dataSource, subGoals);
		}

		private static void CollectGoalPaths(JToken? token, List<string> paths)
		{
			if (token is JObject obj)
			{
				var path = obj["Path"]?.ToString();
				if (path != null && path.EndsWith(".pr")) paths.Add(path);
				foreach (var property in obj.Properties()) CollectGoalPaths(property.Value, paths);
			}
			else if (token is JArray array)
			{
				foreach (var item in array) CollectGoalPaths(item, paths);
			}
		}
	}
}
