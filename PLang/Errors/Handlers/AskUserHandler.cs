using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Interfaces;
using PLang.Services.OutputStream;

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
            (var result, var askError) = await outputStreamFactory.CreateHandler().Ask(error.Message, "ask", 200);
			if (askError != null) return (false, askError);

            return await error.InvokeCallback([result]);

        }
    }
}
