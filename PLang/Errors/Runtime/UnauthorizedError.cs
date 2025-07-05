using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Errors.Runtime
{
	public record UnauthorizedError(string Message = "Unauthorized") : Error(Message, Key: "Unauthorized", StatusCode: 401)
	{
	}
}
