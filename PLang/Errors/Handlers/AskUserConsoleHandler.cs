using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Interfaces;
using PLang.Services.OutputStream;

namespace PLang.Errors.Handlers
{

    public class AskUserConsoleHandler : AskUserHandler
    {
		public AskUserConsoleHandler(IOutputSystemStreamFactory outputStreamFactory) : base(outputStreamFactory)
		{
		}

		public async Task<(bool, IError?)> Handle(AskUser.AskUserError askUserError)
        {
            Console.WriteLine($"\n\n----- Ask User -----\n");
            return await base.Handle(askUserError);
            
        }

    }
}
