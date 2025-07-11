using Fizzler;
using Namotion.Reflection;
using Newtonsoft.Json.Linq;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using Sprache;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueExtractors
{
	public class MathExtractor : IExtractor
	{
		private readonly string op;
		private readonly IEnumerable<object> list;
		private readonly ObjectValue parent;

		public static string[] MathOperators = ["+", "-", "/", "*", "^"];
		public MathExtractor(string op, IEnumerable<object> list, ObjectValue parent)
		{
			this.op = op;
			this.list = list;
			this.parent = parent;
		}

		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			var property = list.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(segment.Value, StringComparison.OrdinalIgnoreCase));
			if (property != null)
			{
				var value = property.GetValue(list);
				return new ObjectValue(segment.Value, value, parent: parent, properties: parent.Properties);
			}


			var firstItem = list.FirstOrDefault();
			if (firstItem != null && firstItem.GetType().IsPrimitive)
			{
				return new ObjectValue(segment.Value, DoOp(list, segment), parent: parent, properties: parent.Properties);
			}

			if (firstItem is IList && firstItem is not JToken)
			{
				List<object> result = new();
				foreach (var item in list)
				{
					if (item is IList list2)
					{
						result.Add(DoOp(list2.Cast<object>(), segment));
					}
				}
				return new ObjectValue(segment.Value, result, parent: parent, properties: parent.Properties);
			}

			if (firstItem is ObjectValue)
			{

				object opResult = DoOp(list.Select(p => ((ObjectValue) p).Value), segment);
				return new ObjectValue(segment.Value, opResult, parent: parent, properties: parent.Properties);

			}

			object? obj = null;
			if (segment.Value.Equals("first", StringComparison.OrdinalIgnoreCase)) obj = list.FirstOrDefault();
			if (segment.Value.Equals("random", StringComparison.OrdinalIgnoreCase)) obj = list.OrderBy(x => Guid.NewGuid()).ToList();
			if (segment.Value.Equals("last", StringComparison.OrdinalIgnoreCase)) obj = list.LastOrDefault();
			if (segment.Value.Equals("count", StringComparison.OrdinalIgnoreCase)) obj = list.Count();
			if (obj != null)
			{
				return new ObjectValue(segment.Value, obj, parent: parent, properties: parent.Properties);
			}

			throw new Exception($"Could not figure out how to calculate {segment.Value} in {parent.PathAsVariable}");
		}


		private object DoOp(IEnumerable<object> value, PathSegment segment)
		{
			Func<object, double> toDouble = x => Convert.ToDouble(x);

			var parts = segment.Value.Split(':', 2, StringSplitOptions.TrimEntries);
			string op = parts[0].ToLowerInvariant();
			string param = parts.Length > 1 ? parts[1] : null;

			var result = op switch
			{
				"sum" => value.Sum(toDouble),
				"avg" => value.Average(toDouble),
				"average" => value.Average(toDouble),
				"mean" => value.Average(toDouble),
				"max" => value.Max(toDouble),
				"min" => value.Min(toDouble),
				"count" => toDouble(value.Count()),
				"first" => value.FirstOrDefault(),
				"last" => value.LastOrDefault(),
				"random" => value.OrderBy(p => Guid.NewGuid()),
				"range" => value.Max(toDouble) - value.Min(toDouble),
				"median" => Median(value, toDouble),
				"mode" => Mode(value, toDouble),
				"percentile" => Percentile(value, toDouble, param),
				"elementat" => value.ElementAt(int.Parse(param ?? "0")),
				"stddev" => StdDev(value, toDouble),
				"variance" => Variance(value, toDouble),
				_ => throw new ArgumentException($"Unknown operation: {op}")
			};

			return result;

		}


		// Helpers
		static double Median(IEnumerable<object> list, Func<object, double> conv)
		{
			var sorted = list.Select(conv).OrderBy(x => x).ToList();
			int count = sorted.Count;
			return count % 2 == 1
				? sorted[count / 2]
				: (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
		}

		static double Mode(IEnumerable<object> list, Func<object, double> conv)
		{
			return list.Select(conv)
				.GroupBy(x => x)
				.OrderByDescending(g => g.Count())
				.ThenBy(g => g.Key)
				.First().Key;
		}

		static double Percentile(IEnumerable<object> list, Func<object, double> conv, string? param)
		{
			if (!double.TryParse(param, out double p)) throw new ArgumentException("Invalid percentile parameter.");
			var sorted = list.Select(conv).OrderBy(x => x).ToList();
			if (sorted.Count == 0) return 0;
			double n = (p / 100.0) * (sorted.Count - 1);
			int k = (int)n;
			double d = n - k;
			return k + 1 < sorted.Count
				? sorted[k] + d * (sorted[k + 1] - sorted[k])
				: sorted[k];
		}

		static double StdDev(IEnumerable<object> list, Func<object, double> conv)
		{
			var values = list.Select(conv).ToList();
			double avg = values.Average();
			double sumSq = values.Sum(x => Math.Pow(x - avg, 2));
			return Math.Sqrt(sumSq / values.Count);
		}

		static double Variance(IEnumerable<object> list, Func<object, double> conv)
		{
			var values = list.Select(conv).ToList();
			double avg = values.Average();
			return values.Sum(x => Math.Pow(x - avg, 2)) / values.Count;
		}

		internal static int IndexOfMathOperator(string value)
		{
			int idx = -1;
			foreach (var op in MathOperators)
			{
				idx = value.IndexOf(op);
				if (idx != -1) return idx;
			}
			return -1;
		}
	}
}
