using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Building.Model
{
	public record RetryHandler(int RetryCount = 3, int RetryDelayInMilliseconds = 50);
}
