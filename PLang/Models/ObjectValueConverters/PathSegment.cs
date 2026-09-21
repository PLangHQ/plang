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
		Path, Index, Method, Math, Property
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
			var regex = new Regex(@"([.!]?)([^.!\[\]]+)|\[(.+?)\]", RegexOptions.Compiled);
			try
			{
				bool propertyStarted = false;
				MatchCollection matches = regex.Matches(path);
				foreach (Match match in matches)
				{
					if (match.Groups[2].Success)
					{
						string value = match.Groups[2].Value.Trim();

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
								
								segments.Add(new PathSegment(value, (match.Groups[1].Value == "!") ? SegmentType.Property : SegmentType.Path));
							}
						}
					}
					else if (match.Groups[3].Success)
					{
						// [3] is a position, [%key%] and [name] are looked up as written or through the memory stack.
						// The extractors read ValueOfPath: a number is a position, anything else is a key.
						string inner = match.Groups[3].Value.Trim();
						object? position;
						if (long.TryParse(inner, out long number))
						{
							position = number;
						}
						else
						{
							string variableName = inner.Trim('%');
							position = memoryStack?.Get<object>(variableName) ?? variableName;
						}
						segments.Add(new PathSegment(inner, SegmentType.Index) { ValueOfPath = position });
					}
				}
			} catch (Exception ex)
			{
				// got stack overflow from regex at one time, seeing if this can catch it? path should be the reason.
				Console.WriteLine($"Exception: {ex.Message} - path:{path}");
				throw;
			}

			// A segment resolved from the memory stack changes between loop iterations, so a path holding one
			// is parsed every time. Cached, %answers[%item.key%]% would keep the key of the first iteration.
			bool hasVariableIndex = segments.Any(s => s.Type == SegmentType.Index && !long.TryParse(s.Value, out _));
			if (!hasVariableIndex) PathCache.TryAdd(path, segments);

			return segments;
		}
	}
}
