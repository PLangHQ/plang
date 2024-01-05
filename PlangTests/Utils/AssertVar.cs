using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLangTests.Utils
{
	public static class AssertVar
	{
		public static void AreEqual(string expected, string actual)
		{
			if (expected.StartsWith("%") && expected.EndsWith("%"))
			{
				expected = expected.Substring(1, expected.Length - 2);
			}
			if (actual.StartsWith("%") && actual.EndsWith("%"))
			{
				actual = actual.Substring(1, actual.Length - 2);
			}

			if (!object.Equals(expected, actual))
			{
				throw new AssertVarException($"AssertVar.AreEqual failed. Expected: <{expected}>, Actual: <{actual}>.");
			}
		}
	}

	public class AssertVarException : Exception
	{
		public AssertVarException(string message) : base(message)
		{
		}
	}

}
