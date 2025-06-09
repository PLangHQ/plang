using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Errors
{
	public interface IErrorReporting
	{
		public int StatusCode { get; set; }
		public string Message { get; set; }
		public object Value { get { return this; } }
		public DateTimeOffset Created { get; init; }
		public List<IError> Errors {  get; init; }
		public List<IError> Error { get; } // errors.first
	}
	public class ErrorReporting
	{
		public static object CreateIssueShouldNotHappen = new
		{
			StatusCode = 501,
			Description = " This should not be happening. Please help us and report the issue here https://github.com/PLangHQ/plang/issues",
		};
		
		public static object CreateIssueNotImplemented = new
		{
			StatusCode = 501,
			Description = "This hasn't been implemented. Please help us and report the issue here https://github.com/PLangHQ/plang/issues or add the feature yourself and push PR if you know how to.",
		};
	}
}
