using PLang.Errors.AskUser;
using PLang.Exceptions.AskUser;

namespace PLang.Errors.Handlers
{

    public abstract class BaseErrorHandler
	{
		
		public BaseErrorHandler()
		{
		
		}
		public async Task<(bool, IError?)> Handle(IError error)
		{
			Console.WriteLine(error.MessageOrDetail);

			throw new NotImplementedException();
			/*
			if (error is not AskUser.AskUserError aue) return (false, error);

			var result = await askUserHandlerFactory.CreateHandler().Handle(aue);
			if (result.Item2 is PLang.Errors.AskUser.AskUserError aue2)
			{
				return await Handle(result.Item2);
			}
			return result;
			*/
		}

	}
}
