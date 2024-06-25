using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Exceptions.AskUser;

namespace PLang.Interfaces
{
    public interface IAskUserHandler
	{
		// return true if program should continue where it threw the error
		// return false if the progam should end
		Task<(bool, IError?)> Handle(AskUserError ex);
	}
}
