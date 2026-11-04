using PLang.Building.Model;
using System.Text;
using static PLang.Modules.WebserverModule.Program;

namespace PLang.Modules.WebserverModule
{
	public class RouteDocs
	{
		private const int MaxSummaryLength = 160;
		private const int MaxSubGoalDepth = 3;

		private readonly Func<Routing, Goal?> getGoal;
		private readonly Func<string, Goal?> getSubGoal;

		private readonly Func<Goal, string?> getDescription;

		private readonly Func<Goal, EffectSummary> getEffects;

		public RouteDocs(Func<Routing, Goal?> getGoal, Func<string, Goal?> getSubGoal, Func<Goal, string?> getDescription, Func<Goal, EffectSummary> getEffects)
		{
			this.getGoal = getGoal;
			this.getSubGoal = getSubGoal;
			this.getDescription = getDescription;
			this.getEffects = getEffects;
		}

		public string Build(List<Routing> routings, string requestPath)
		{
			var path = NormalizePath(requestPath);
			var prefix = path == "/" ? "" : path;

			var exact = routings.Where(r => r.Route?.PathRegex?.IsMatch(path) == true).ToList();
			var children = routings
				.Where(r => !exact.Contains(r) && r.Path.StartsWith(prefix + "/", StringComparison.Ordinal))
				.OrderBy(r => r.Path, StringComparer.Ordinal)
				.ToList();

			var sb = new StringBuilder();
			if (exact.Count == 0 && children.Count == 0)
			{
				sb.AppendLine($"# {path}?");
				sb.AppendLine();
				sb.AppendLine("No routes found under this path.");
				return sb.ToString();
			}

			foreach (var routing in exact)
			{
				AppendDetail(sb, routing);
			}

			if (children.Count > 0)
			{
				AppendChildren(sb, children, prefix, path);
			}

			return sb.ToString();
		}

		private void AppendDetail(StringBuilder sb, Routing routing)
		{
			var methods = string.Join(", ", routing.RequestProperties?.Methods ?? ["GET"]);
			sb.AppendLine($"# {methods} {routing.Path}");
			sb.AppendLine();

			var goal = getGoal(routing);
			if (goal == null)
			{
				sb.AppendLine("Goal: " + routing.Route?.Goal?.Name + " (no build output found)");
				sb.AppendLine();
				return;
			}

			sb.AppendLine($"Goal: {goal.RelativeGoalPath}");
			if (routing.ResponseProperties?.ContentType != null)
			{
				sb.AppendLine($"Response content type: {routing.ResponseProperties.ContentType}");
			}
			sb.AppendLine();

			var parameters = routing.Route?.ParamInfos ?? new();
			if (parameters.Count > 0)
			{
				sb.AppendLine("## Path parameters");
				foreach (var p in parameters)
				{
					sb.AppendLine($"- {p.Name}" + (string.IsNullOrEmpty(p.Type) ? "" : $" ({p.Type})"));
				}
				sb.AppendLine();
			}

			var effects = getEffects(goal);
			sb.AppendLine("## Effects: " + effects.Tag());
			foreach (var write in effects.DbWrites) sb.AppendLine($"- writes database: {write}");
			foreach (var item in effects.Files) sb.AppendLine($"- writes files: {item}");
			foreach (var item in effects.External) sb.AppendLine($"- external call: {item}");
			foreach (var item in effects.Costs) sb.AppendLine($"- costs money: {item}");
			foreach (var item in effects.Opaque) sb.AppendLine($"- opaque, can do anything: {item}");
			foreach (var item in effects.Unknown) sb.AppendLine($"- not declared: {item}");
			sb.AppendLine();

			var description = getDescription(goal);
			if (!string.IsNullOrWhiteSpace(description))
			{
				sb.AppendLine("## What it does");
				sb.AppendLine(Clean(description));
				sb.AppendLine();
			}

			var variables = new Dictionary<string, string>();
			CollectVariables(goal, variables, 0);
			var fromRequest = variables.Where(v => v.Key.StartsWith("%request.")).OrderBy(v => v.Key, StringComparer.Ordinal).ToList();
			if (fromRequest.Count > 0)
			{
				sb.AppendLine("## Request values the goal reads");
				foreach (var v in fromRequest)
				{
					sb.AppendLine($"- {v.Key}: {Clean(v.Value)}");
				}
				sb.AppendLine();
			}
		}

		private void CollectVariables(Goal goal, Dictionary<string, string> variables, int depth)
		{
			foreach (var item in goal.IncomingVariablesRequired ?? new())
			{
				variables.TryAdd(item.Key, item.Value);
			}

			if (depth >= MaxSubGoalDepth) return;
			foreach (var subGoalPath in goal.SubGoals ?? new())
			{
				var subGoal = getSubGoal(subGoalPath);
				if (subGoal != null) CollectVariables(subGoal, variables, depth + 1);
			}
		}

		private void AppendChildren(StringBuilder sb, List<Routing> children, string prefix, string path)
		{
			var below = children
				.Select(r => (Routing: r, Tokens: r.Path.Substring(prefix.Length + 1).Split('/')))
				.ToList();
			var leaves = below.Where(b => b.Tokens.Count(t => !t.StartsWith('%')) <= 1).ToList();
			var folders = below
				.Where(b => b.Tokens.Count(t => !t.StartsWith('%')) > 1)
				.GroupBy(b => b.Tokens[0])
				.OrderBy(g => g.Key, StringComparer.Ordinal)
				.ToList();

			sb.AppendLine($"# Under {path}");
			sb.AppendLine();

			if (folders.Count > 0)
			{
				sb.AppendLine("## Folders");
				foreach (var folder in folders)
				{
					var folderPath = prefix + "/" + folder.Key;
					sb.AppendLine($"- {folderPath}/ ({folder.Count()} routes): `GET {folderPath}?`");
				}
				sb.AppendLine();
			}

			if (leaves.Count > 0)
			{
				sb.AppendLine("## Routes here");
				foreach (var leaf in leaves) AppendLine(sb, leaf.Routing);
				sb.AppendLine();
			}

			sb.AppendLine("Add `?` to a folder or route path to see what is under it or the details.");
		}

		private void AppendLine(StringBuilder sb, Routing routing)
		{
			var methods = string.Join("/", routing.RequestProperties?.Methods ?? ["GET"]);
			var goal = getGoal(routing);
			var summary = Summary(goal == null ? null : getDescription(goal));
			var tag = goal == null ? "" : $" [{getEffects(goal).Tag()}]";
			sb.AppendLine($"- {methods} {routing.Path}{tag}" + (summary == "" ? "" : ": " + summary));
		}

		private static string Summary(string? description)
		{
			if (string.IsNullOrWhiteSpace(description)) return "";
			var text = Clean(description);
			var end = text.IndexOf(". ", StringComparison.Ordinal);
			if (end > 0) text = text.Substring(0, end + 1);
			if (text.Length > MaxSummaryLength) text = text.Substring(0, MaxSummaryLength - 3) + "...";
			return text;
		}

		private static string Clean(string text) => text.Replace("\\%", "%").Replace("\n", " ").Trim();

		private static string NormalizePath(string? path)
		{
			if (string.IsNullOrEmpty(path)) return "/";
			path = path.TrimEnd('/');
			return path == "" ? "/" : path;
		}

	}
}
