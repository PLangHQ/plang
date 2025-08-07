using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Errors.Runtime
{
	public record NotFoundError(string Message = "Not found", string Key = "NotFound", string? FixSuggestion = null) 
		: Error(Message, Key: Key, StatusCode: 404, FixSuggestion: FixSuggestion)
	{
	}
}
