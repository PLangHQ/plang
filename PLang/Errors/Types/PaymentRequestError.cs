using Nethereum.Contracts.QueryHandlers.MultiCall;
using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Errors.Interfaces;
using PLang.Models;
using PLang.Modules;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Utils.StepHelper;

namespace PLang.Errors.Types
{

	public interface IExternalCallbackError { 
		public Callback? Callback {  get; }
	}

	public record PaymentContract(string name, string description, string error, List<Dictionary<string, object>> services, Signature? Client = null, Signature? Service = null);
	public record PaymentRequest(PaymentContract contract, Callback Callback);

	public record PaymentRequestError(GoalStep GoalStep, PaymentContract Contract, string Message, string Key = "PaymentRequest", int StatusCode = 402, 
			Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null, Callback? Callback = null)
			: Error(Message, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks, Contract), IError, IUserDefinedError, IBuilderError, IExternalCallbackError
	{
		public bool Retry => false;
		public bool ContinueBuild => false;
		public string? LlmBuilderHelp { get; set; }

		public override PaymentRequest AsData()
		{
			
			return new PaymentRequest(Contract, Callback);
			
		}
	}
}
