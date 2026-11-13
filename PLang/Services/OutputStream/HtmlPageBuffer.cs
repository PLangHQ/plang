using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using PLang.Services.OutputStream.Messages;

namespace PLang.Services.OutputStream;

/// <summary>
/// Holds the page of a classic (non ajax) GET while the goal runs, and applies every following
/// message to it the way plang js applies it in the browser: a render with a target and an action
/// (append, replace, prepend ...) changes that element, an untargeted one changes #main.
/// The finished document is written once, when the request ends.
/// </summary>
/// <remarks>
/// The first render or ask that carries a whole document (the layout wrapped around it) starts the
/// page. Anything else, a text message first or a fragment without a layout, leaves the request in
/// direct mode, where messages are written as they come, as before this class existed.
/// </remarks>
public sealed class HtmlPageBuffer
{
	static readonly HashSet<string> ContentActions = new(StringComparer.Ordinal)
	{
		"replace", "replaceSelf", "append", "appendOrReplace", "prepend", "prependOrReplace"
	};

	readonly object gate = new();
	readonly HtmlParser parser = new();
	readonly List<ExecuteMessage> executes = new();

	string? first;
	int firstStatusCode = 200;
	IDocument? document;
	bool direct;

	public bool Started { get; private set; }

	public int StatusCode => firstStatusCode;

	/// <summary>
	/// Takes the message into the page. False means the message is not the page's to keep and must be
	/// written to the response directly, after FlushAsync of whatever the page already holds.
	/// </summary>
	public bool TryAdd(OutMessage message)
	{
		lock (gate)
		{
			if (direct) return false;

			if (message is ExecuteMessage execute)
			{
				if (!Started) return false;
				executes.Add(execute);
				return true;
			}

			var content = GetContent(message);
			if (content == null) return false;

			if (!Started)
			{
				if (message is TextMessage || !LooksLikeDocument(content))
				{
					direct = true;
					return false;
				}

				first = content;
				firstStatusCode = message.StatusCode == 0 ? 200 : message.StatusCode;
				Started = true;
				return true;
			}

			document ??= parser.ParseDocument(first!);
			Apply(message, content);
			return true;
		}
	}

	/// <summary>The finished html and the execute messages that belong after it, or null when nothing is held.</summary>
	public (string Html, List<ExecuteMessage> Executes)? Take()
	{
		lock (gate)
		{
			if (!Started || direct) return null;

			var html = document?.ToHtml() ?? first!;
			var taken = (html, executes.ToList());
			executes.Clear();
			first = null;
			document = null;
			direct = true;
			return taken;
		}
	}

	public void MarkDirect()
	{
		lock (gate) { direct = true; }
	}

	static string? GetContent(OutMessage message) => message switch
	{
		RenderMessage r => r.Content,
		AskMessage a => a.Content,
		TextMessage t => t.Content,
		_ => null
	};

	static bool LooksLikeDocument(string content) => content.Contains("<html", StringComparison.OrdinalIgnoreCase);

	void Apply(OutMessage message, string content)
	{
		var actions = (message.Actions ?? Array.Empty<string>()).Where(ContentActions.Contains).ToList();
		if (actions.Count == 0) actions.Add(message is TextMessage ? "append" : "replace");

		var selector = string.IsNullOrEmpty(message.Target) ? "#main" : message.Target;
		IHtmlCollection<IElement> targets;
		try
		{
			targets = document!.QuerySelectorAll(selector);
		}
		catch (Exception)
		{
			return;
		}
		if (targets.Length == 0) return;

		foreach (var action in actions)
		{
			foreach (var container in targets.ToList())
			{
				switch (action)
				{
					case "replace":
						container.InnerHtml = content;
						break;
					case "replaceSelf":
						container.OuterHtml = content;
						break;
					default:
						AddNodes(container, content, action);
						break;
				}
			}
		}
	}

	void AddNodes(IElement container, string content, string action)
	{
		var nodes = parser.ParseFragment(content, container).ToArray();

		if (action.EndsWith("OrReplace", StringComparison.Ordinal))
		{
			var id = nodes.OfType<IElement>().FirstOrDefault()?.Id;
			if (!string.IsNullOrEmpty(id))
			{
				foreach (var existing in container.QuerySelectorAll("#" + id).ToList()) existing.Remove();
			}
		}

		if (action.StartsWith("append", StringComparison.Ordinal)) container.Append(nodes);
		else container.Prepend(nodes);
	}
}
