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
            var result = await outputStreamFactory.CreateHandler().Ask(error.Message, "ask", 200);

            return await error.InvokeCallback([result]);

        }
    }
}
