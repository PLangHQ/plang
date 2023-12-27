using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Building.Model
{
	public enum ExpirationType
	{
		AbsoluteExpiration = 0,
		AbsoluteExpirationRelativeToNow = 1,
		SlidingExpiration = 2
	}

	public record Caching(ExpirationType ExpirationType, string? expiresAtDateTime = null, long? milliseconds = null)
	{
	}
}
