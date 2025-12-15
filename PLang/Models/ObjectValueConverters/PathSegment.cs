using PLang.Models.ObjectValueExtractors;
using PLang.Runtime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueConverters
{

	public enum SegmentType
	{
		Path, Index, Method, Math
	}
	public record PathSegment(string Value, SegmentType Type)
	{
		public object? ValueOfPath;
	};

	public static class PathSegmentParser
	{
		static ConcurrentDictionary<string, List<PathSegment>> PathCache = new();

		public static List<PathSegment> ParsePath(string path, MemoryStack? memoryStack = null)
		{
			if (PathCache.TryGetValue(path, out var pathSegments)) return pathSegments;

			var segments = new List<PathSegment>();

			var newRegex = new Regex(@"
    ([A-Za-z_][A-Za-z0-9_]*\((?>[^()]+|\((?<DEPTH>)|\)(?<-DEPTH>))*(?(DEPTH)(?!))\)) # method call
  | ([A-Za-z_][A-Za-z0-9_]*)                                                       # identifier
  | \[(.+?)\]                                                                      # indexer
", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
			MatchCollection matches2 = newRegex.Matches(path);

			foreach (Match match in matches2)
			{
				// method
				if (match.Groups[1].Success)
				{
					segments.Add(new PathSegment(match.Groups[1].Value, SegmentType.Method));
					continue;
				}

				// identifier
				if (match.Groups[2].Success)
				{
					segments.Add(new PathSegment(match.Groups[2].Value, SegmentType.Path));
					continue;
				}

				// index
				if (match.Groups[3].Success)
				{
					var idx = match.Groups[3].Value;
					var position = memoryStack.Get<object>(idx);
					segments.Add(new PathSegment(idx, SegmentType.Index) { ValueOfPath = position });
					continue;
				}
			}


			PathCache.TryAdd(path, segments);

			return segments;


			/*






			var regex = new Regex(@"([^[.\]]+)|\[(.+?)\]", RegexOptions.Compiled);
			try
			{
				MatchCollection matches = regex.Matches(path);
				foreach (Match match in matches)
				{
					if (match.Groups[1].Success)
					{
						string value = match.Groups[1].Value.Trim();

						bool openBracket = value.Contains('(');
						bool closeBracket = value.Contains(')');
						if (openBracket && closeBracket)
						{
							segments.Add(new PathSegment(value, SegmentType.Method));
						}
						else if (openBracket || closeBracket)
						{
							throw new Exception($"{value} is not a valid variable. You must have both open and close the paranthese");
						}
						else
						{
							int startIndex = MathExtractor.IndexOfMathOperator(value);
							if (startIndex != -1)
							{
								string pathValue = value[..startIndex];
								string math = value[startIndex..];

								if (!string.IsNullOrEmpty(pathValue.Trim()))
								{
									segments.Add(new PathSegment(pathValue.Trim(), SegmentType.Path));
								}
								segments.Add(new PathSegment(math.Trim(), SegmentType.Math));
							}
							else
							{
								segments.Add(new PathSegment(value, SegmentType.Path));
							}
						}
					}
					else if (match.Groups[2].Success)
					{
						var position = memoryStack.Get<object>(match.Groups[2].Value);
						segments.Add(new PathSegment(match.Groups[2].Value, SegmentType.Index) {  ValueOfPath = position });
					}
				}
			} catch (Exception ex)
			{
				// got stack overflow from regex at one time, seeing if this can catch it? path should be the reason.
				Console.WriteLine($"Exception: {ex.Message} - path:{path}");
				throw;
			}

			PathCache.TryAdd(path, segments);

			return segments;*/
		}
	}
}
