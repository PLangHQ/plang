using PLang.Building.Model;
using System.Text;
using static PLang.Modules.WebserverModule.Program;

namespace PLang.Modules.WebserverModule
{
	public class RouteDocs
	{
		private const int MaxSummaryLength = 160;

		private static readonly Dictionary<string, string> EffectMeaning = new()
		{
			["read"] = "changes nothing",
			["changes data"] = "creates, updates or deletes data, or writes files",
			["external"] = "contacts something outside the system, for example sends an email or calls another service",
			["costs"] = "uses a paid service such as an AI model",
			["opaque"] = "can do anything, be careful",
			["unknown"] = "what it does is not declared"
		};
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

		// What an assistant with no key reads when it asks for the documentation. It shows no routes, only how to
		// make a key, get it registered and sign requests, so it is safe to give to anyone.
		public static string ConnectInstructions(string baseUrl)
		{
			return ConnectText.Replace("{{BASE}}", baseUrl);
		}

		private const string ConnectText = """
# Connecting to this app

This app describes itself to an AI assistant. Every request must be signed with an Ed25519 key that belongs to you. There is no password, only a signature made on your own computer. The private key never leaves your computer.

You need OpenSSL 3 (`openssl version` should say OpenSSL 3, not LibreSSL; on a Mac install it with brew) and curl.

## 1. Make your key, once

```bash
mkdir -p ~/.plang-app
[ -f ~/.plang-app/key.pem ] || { openssl genpkey -algorithm ed25519 -out ~/.plang-app/key.pem && chmod 600 ~/.plang-app/key.pem; }
openssl pkey -in ~/.plang-app/key.pem -pubout -outform DER | tail -c 32 | base64 | tr -d '\n'; echo
```

The key is only made if it does not exist yet, so an existing key is never overwritten. The last line prints your public key. It is safe to share.

## 2. Get the key registered

Show the public key to the person you work for and ask them to send it to the administrator of this app (whoever gave them this address), who links it to their user. Until that is done every request answers with no access. This is the one step that needs a human, so stop here and wait.

To check whether it is done, run the signed request in step 4. If it answers with the documentation, the key is registered. If it answers 403 or nothing, it is not yet.

## 3. Sign every request

```bash
KEY=~/.plang-app/key.pem
psign() {  # psign METHOD URL [BODY]
  local METHOD="$1" URL="$2" BODY="$3"
  local PUB NONCE CREATED DATA HASH MSG TMP SIG SIGNED
  PUB=$(openssl pkey -in "$KEY" -pubout -outform DER | tail -c 32 | base64 | tr -d '\n')
  NONCE=$( (uuidgen 2>/dev/null || cat /proc/sys/kernel/random/uuid) | tr 'A-Z' 'a-z')
  CREATED=$(date -u +%Y-%m-%dT%H:%M:%S.000Z)
  DATA=null
  if [ -n "$BODY" ]; then
    HASH=$(printf '%s' "$BODY" | openssl dgst -sha256 -hex | sed 's/^.*= //')
    DATA="{\"type\":\"sha256\",\"format\":\"json\",\"hash\":\"$HASH\"}"
  fi
  MSG="{\"type\":\"Ed25519\",\"nonce\":\"$NONCE\",\"created\":\"$CREATED\",\"expires\":null,\"name\":null,\"data\":$DATA,\"contracts\":[\"C0\"],\"headers\":null,\"parent\":null,\"identity\":\"$PUB\",\"signature\":null}"
  TMP=$(mktemp); printf '%s' "$MSG" > "$TMP"
  SIG=$(openssl pkeyutl -sign -inkey "$KEY" -rawin -in "$TMP" | base64 | tr -d '\n'); rm -f "$TMP"
  SIGNED="${MSG/\"signature\":null/\"signature\":\"$SIG\"}"
  if [ -n "$BODY" ]; then
    curl -sS -X "$METHOD" -H "X-Signature: $SIGNED" --data-binary "$BODY" "$URL"
  else
    curl -sS -X "$METHOD" -H "X-Signature: $SIGNED" "$URL"
  fi
}
```

Each signature is good for 5 minutes and one request. The signed text must keep exactly this property order. For a request with a body, pass the body as the third argument so it is covered by the signature; a body changed in transit is then refused.

## 4. Explore

```bash
psign GET "{{BASE}}/?"
```

Add `?` to a path to see what is under it: `psign GET "{{BASE}}/admin?"` lists the folders, and `psign GET "{{BASE}}/admin/crm?"` goes one level down. A path to one route shows how to call it, what it changes and what it costs. You only see what your user may run.

Every route in a listing has a marker in square brackets:

* `[read]` changes nothing and is safe to call.
* `[changes data]` creates, updates or deletes something.
* `[external]` contacts something outside, for example sends an email to a company.
* `[costs]` uses a paid service such as an AI model.
* `[opaque]` can do anything. `[unknown]` is not declared.

Markers can be combined, for example `[changes data; costs]`. Before you call something that has any marker other than `read`, tell the person what you are about to do and ask them first. Never call anything marked `opaque` or `unknown` without asking. Note that some routes marked `read` for a person looking at a page can still be listed with other markers, because opening them starts work in the background. Trust the marker, not the name.
""";

		// One goal from each folder that has a route shown for this path. The before events of a goal decide
		// whether the caller may run it, and an app binds them to folders (a login check on one folder, a
		// role check on another), so one goal per folder stands for all the goals in it.
		public List<Goal> GoalsToAuthorize(List<Routing> routings, string requestPath)
		{
			var path = NormalizePath(requestPath);
			var prefix = path == "/" ? "" : path;

			return routings
				.Where(r => r.Route?.PathRegex?.IsMatch(path) == true || r.Path.StartsWith(prefix + "/", StringComparison.Ordinal))
				.Select(getGoal)
				.Where(g => g != null)
				.Select(g => g!)
				.GroupBy(g => g.RelativeGoalFolderPath)
				.Select(g => g.First())
				.ToList();
		}

		// Null when the path has routes but the caller may not run any of them.
		public string? Build(List<Routing> routings, string requestPath, Func<Goal, bool> allowed)
		{
			var path = NormalizePath(requestPath);
			var prefix = path == "/" ? "" : path;

			var exact = routings.Where(r => r.Route?.PathRegex?.IsMatch(path) == true).ToList();
			var children = routings
				.Where(r => !exact.Contains(r) && r.Path.StartsWith(prefix + "/", StringComparison.Ordinal))
				.OrderBy(r => r.Path, StringComparer.Ordinal)
				.ToList();

			bool IsAllowed(Routing routing)
			{
				var goal = getGoal(routing);
				return goal != null && allowed(goal);
			}

			var existed = exact.Count + children.Count > 0;
			exact = exact.Where(IsAllowed).ToList();
			children = children.Where(IsAllowed).ToList();
			if (existed && exact.Count + children.Count == 0) return null;

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
			foreach (var tag in effects.Tag().Split("; "))
			{
				if (EffectMeaning.TryGetValue(tag, out var meaning)) sb.AppendLine($"- {tag}: {meaning}");
			}
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
