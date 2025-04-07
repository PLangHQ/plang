using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Errors
{
	public record PaymentError : Error, IError
	{
		public PaymentError(string Message, string Type = "onetime", Dictionary<string, object>? currenciesWithParameters = null, Dictionary<string, object>? Contracts = null, 
				string Key = "PaymentError", int StatusCode = 402, 
				Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null) 
			: base(Message, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
		{
		}
	}
}
