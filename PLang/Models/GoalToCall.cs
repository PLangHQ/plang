using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models
{
	public class GoalToCall
	{
		public string? Value { get; }

		public GoalToCall(string? value)
		{
			
			if (!string.IsNullOrWhiteSpace(value))
			{
				if (value.Contains("\\"))
				{
					value = value.Replace("\\", "/");
				}

				Value = value.Replace("!", "");
			}
			
		}

		public override string? ToString() => Value;

		// Implicit conversion from string to GoalToCall
		public static implicit operator GoalToCall(string? value) => new GoalToCall(value);

		// Implicit conversion from GoalToCall to string
		public static implicit operator string?(GoalToCall? goalToCall) => goalToCall?.Value;

		public override bool Equals(object? obj)
		{
			if (obj is GoalToCall other)
			{
				return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		public override int GetHashCode() => Value?.GetHashCode() ?? 0;
	}
}
