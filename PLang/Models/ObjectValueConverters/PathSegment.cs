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
						
						if (MathExtractor.MathOperators.Select(p => value.Contains(p)).Any())
						{
							int startIndex = -1;
							for (int i=0;i<value.Length;i++) 
							{
								if (MathExtractor.MathOperators.Contains(value[i].ToString()))
								{
									startIndex = i;
								}
							}
							if (startIndex == -1)
							{
								segments.Add(new PathSegment(value, SegmentType.Path));
								continue;
							}


							string pathValue = value.Substring(0, startIndex);
							string math = value.Substring(startIndex);

							segments.Add(new PathSegment(pathValue.Trim(), SegmentType.Path));
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

			return segments;
		}
	}
}
