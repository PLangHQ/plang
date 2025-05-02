using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models
{
	public interface IProperties : IDictionary<string, object>
	{
	}

	public class Properties : Dictionary<string, object>, IProperties
	{
	}
}
