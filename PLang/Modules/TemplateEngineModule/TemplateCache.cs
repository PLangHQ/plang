using Scriban;
using System.Collections.Concurrent;

namespace PLang.Modules.TemplateEngineModule
{
	/// <summary>
	/// Process wide cache of parsed Scriban templates.
	///
	/// Parsing a 250 KB page template costs milliseconds and a lot of garbage, and the same file was
	/// parsed again on every render, including every nested `render` call. A parsed Template is
	/// an immutable syntax tree: rendering it only reads the tree and writes into the TemplateContext
	/// that is passed to RenderAsync. All request specific state (variables, functions, output) lives
	/// in that per render TemplateContext, never in the cached object.
	///
	/// The cache is keyed by the template text itself, so an edited file or a hot reload produces a
	/// different key and the old entry just ages out. File reads are skipped with a second map keyed by
	/// full path and last write time.
	/// </summary>
	public static class TemplateCache
	{
		private const int MaxEntries = 256;
		private const int MaxContentLength = 2_000_000;

		private static readonly ConcurrentDictionary<string, Template> parsed = new(StringComparer.Ordinal);
		private static readonly ConcurrentDictionary<string, (long Ticks, string Content)> files = new(StringComparer.Ordinal);

		public static Template GetOrParse(string content)
		{
			if (content.Length > MaxContentLength) return Template.Parse(content);

			if (parsed.TryGetValue(content, out var template)) return template;

			template = Template.Parse(content);
			if (parsed.Count >= MaxEntries) parsed.Clear();
			parsed[content] = template;
			return template;
		}

		public static bool TryGetFileContent(string fullPath, long lastWriteTicks, out string content)
		{
			if (files.TryGetValue(fullPath, out var entry) && entry.Ticks == lastWriteTicks)
			{
				content = entry.Content;
				return true;
			}
			content = "";
			return false;
		}

		public static void SetFileContent(string fullPath, long lastWriteTicks, string content)
		{
			if (content.Length > MaxContentLength) return;
			if (files.Count >= MaxEntries) files.Clear();
			files[fullPath] = (lastWriteTicks, content);
		}
	}
}
