using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	public static class DateTimeExtension
	{
		public static long GetUnixTime(this DateTime dateTime)
		{
			return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
		}
	}
}
