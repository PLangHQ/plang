using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.ObjectTypes
{
	public class HtmlType
	{
		public HtmlType(string html)
		{
			Value = html;
		}

		public string Value { get; }
		public override string ToString()
		{
			return Value;
		}
	}
}
