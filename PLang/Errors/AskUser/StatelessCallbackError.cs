using static PLang.Utils.StepHelper;

namespace PLang.Errors.AskUser
{
	public record StatelessCallbackError : Error, IErrorHandled
	{
		public StatelessCallbackError(Callback callback, string message = "Callback error", int statusCode = 400) : base(message, "CallbackError", statusCode)
		{
			Callback = callback;
		}

		public Callback Callback { get; }

		public bool IgnoreError => true;

		public IError? InitialError => null;
	}
}
