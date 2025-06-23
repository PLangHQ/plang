using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Errors.Runtime
{
	public record NotFoundError(string Message = "Not found") : Error(Message, Key: "NotFound", StatusCode: 404)
	{
	}
}
