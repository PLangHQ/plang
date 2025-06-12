using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using NCalc;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NBitcoin.Protocol;

namespace PLang.Models.ObjectValueExtractors
{
	public class MathExpressionExtractor : IExtractor
	{
		private readonly string expression;
		private readonly ObjectValue parent;

		public MathExpressionExtractor(string expression, ObjectValue parent)
		{
			this.expression = expression;
			this.parent = parent;
		}

		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			if (parent.Value.GetType().FullName.StartsWith("System.DateTime"))
			{
				ObjectValue? ov = null;
				if (parent.Value is DateTimeOffset dto)
				{
					var newDt = DoDateTimeOp(dto, segment);
					ov = new ObjectValue(segment.Value, newDt, parent: parent, properties: parent.Properties);
				}
				else if (parent.Value is DateTime dt)
				{
					var newDt = DoDateTimeOp(dt, segment);
					ov = new ObjectValue(segment.Value, newDt, parent: parent, properties: parent.Properties);
				} else
				{
					throw new Exception($"{parent.Value} is not supported for datetime operation");
				}

				return ov;
			}

			try
			{
				var expressionObj = new NCalc.Expression(parent.Value + expression.Replace(",", "."));
				var result = expressionObj.Evaluate();


				return new ObjectValue(segment.Value, result, parent: parent, properties: parent.Properties);
			}
			catch (Exception ex)
			{
				return new ObjectValue(segment.Value, null, parent: parent, properties: parent.Properties);
			}
		}


		private DateTimeOffset DoDateTimeOp(DateTimeOffset dt, PathSegment segment)
		{
			var regex = new Regex(@"([0-9]+)\s*([a-zA-Z]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
			var matches = regex.Matches(segment.Value);
			if (matches.Count == 0 || matches[0].Groups.Count != 3) return dt;

			int multiplier = (segment.Value.Contains("+")) ? 1 : -1;
			string number = matches[0].Groups[1].Value;
			string function = matches[0].Groups[2].Value;


			if (!int.TryParse(number, out int intValue))
			{
				return dt;
			}

			if (function == "micro")
			{
				return dt.AddMicroseconds(multiplier * intValue);
			}
			if (function == "ms")
			{
				return dt.AddMilliseconds(multiplier * intValue);
			}
			if (function.StartsWith("sec"))
			{
				return dt.AddSeconds(multiplier * intValue);
			}
			if (function.StartsWith("min"))
			{
				return dt.AddMinutes(multiplier * intValue);
			}
			if (function.StartsWith("hour"))
			{
				return dt.AddHours(multiplier * intValue);
			}
			if (function.StartsWith("day"))
			{
				return dt.AddDays(multiplier * intValue);
			}
			if (function.StartsWith("month"))
			{
				return dt.AddMonths(multiplier * intValue);
			}
			if (function.StartsWith("year"))
			{
				return dt.AddYears(multiplier * intValue);
			}


			return dt;
		}
	}
}
