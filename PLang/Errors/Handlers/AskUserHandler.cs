using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Interfaces;
using PLang.Services.OutputStream;
using static PLang.Modules.OutputModule.Program;

namespace PLang.Errors.Handlers
{
    public class AskUserHandler : IAskUserHandler
    {
        private readonly IOutputSystemStreamFactory outputStreamFactory;

        public AskUserHandler(IOutputSystemStreamFactory outputStream)
        {
            this.outputStreamFactory = outputStream;
        }


        public async Task<(bool, IError?)> Handle(AskUser.AskUserError error)
        {
			var askOptions = new AskOptions(error.Message);

            (var result, var askError) = await outputStreamFactory.CreateHandler().Ask(askOptions);
			if (askError != null) return (false, askError);

            return await error.InvokeCallback([result]);

        }
    }
}
