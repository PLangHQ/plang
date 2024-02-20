using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Utils;
using System;

namespace PLang.Exceptions.Handlers
{

	public abstract class ExceptionHandler
	{
		private readonly IAskUserHandlerFactory askUserHandlerFactory;

		public ExceptionHandler(IAskUserHandlerFactory askUserHandlerFactory)
		{
			this.askUserHandlerFactory = askUserHandlerFactory;
		}
		public async Task<bool> Handle(Exception exception)
		{
			var ex = (exception.InnerException != null) ? exception.InnerException : exception;
			
			if (ex is AskUserException aue)
			{
				try
				{
					return await askUserHandlerFactory.CreateHandler().Handle(aue);
				}
				catch (AskUserException ex2)
				{
					return await Handle(ex2);
				}
			}
			
			return false;
		}

	}
}
