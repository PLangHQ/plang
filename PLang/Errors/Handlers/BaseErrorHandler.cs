using PLang.Errors.AskUser;
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
		public async Task<(bool, IError?)> Handle(IError error)
		{
			if (error is not AskUser.AskUserError aue) return (false, error);

			return await askUserHandlerFactory.CreateHandler().Handle(aue);

		}

	}
}
