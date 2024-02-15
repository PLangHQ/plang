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
		private readonly IAskUserHandler askUserHandler;

		public ExceptionHandler(IAskUserHandler askUserHandler)
		{
			this.askUserHandler = askUserHandler;
		}
		public async Task<bool> Handle(Exception exception)
		{
			var ex = exception;
			while (ex != null && ex.InnerException != null)
			{
				ex = exception.InnerException;
			}

			if (ex is MissingSettingsException mse)
			{
				return await askUserHandler.Handle(mse);
			}
			if (ex is AskUserException ase)
			{
				return await askUserHandler.Handle(ase);
			}
			return false;
		}

	}
}
