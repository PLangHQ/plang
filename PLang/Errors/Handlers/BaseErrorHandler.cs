using PLang.Exceptions.AskUser;

namespace PLang.Errors.Handlers
{

	public abstract class BaseErrorHandler
	{
		private readonly IAskUserHandlerFactory askUserHandlerFactory;

		public BaseErrorHandler(IAskUserHandlerFactory askUserHandlerFactory)
		{
			this.askUserHandlerFactory = askUserHandlerFactory;
		}
		public async Task<bool> Handle(IError error)
		{
			if (error is not AskUserError aue) return false;

			return await askUserHandlerFactory.CreateHandler().Handle(aue);

		}

	}
}
