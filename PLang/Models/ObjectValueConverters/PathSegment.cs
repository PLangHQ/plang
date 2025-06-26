using PLang.Models.ObjectValueExtractors;
using System;
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
	public record PathSegment(string Value, SegmentType Type);

	public static class PathSegmentParser
	{
		public static List<PathSegment> ParsePath(string path)
		{
			var segments = new List<PathSegment>();
			var regex = new Regex(@"([^[.\]]+)|\[(.+?)\]", RegexOptions.Compiled);
			try
			{
				foreach (Match match in regex.Matches(path))
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
						segments.Add(new PathSegment(match.Groups[2].Value, SegmentType.Index));
					}
				}
			} catch (Exception ex)
			{
				// got stack overflow from regex at one time, seeing if this can catch it? path should be the reason.
				Console.WriteLine($"Exception: {ex.Message} - path:{path}");
				throw;
			}

			return segments;
		}
	}
}
