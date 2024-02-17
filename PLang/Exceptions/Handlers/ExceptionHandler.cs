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
				ex = ex.InnerException;
			}

			if (ex is MissingSettingsException mse)
			{
				try
				{
					return await askUserHandler.Handle(mse);
				}
				catch (AskUserException ex2)
				{
					return await Handle(ex2);
				}
			}
			if (ex is AskUserException ase)
			{
				try
				{
					var result = await askUserHandler.Handle(ase);
					return result;
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
