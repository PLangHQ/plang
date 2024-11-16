﻿using PLang.Exceptions.AskUser;

namespace PLang.Errors.Handlers;

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

        var result = await askUserHandlerFactory.CreateHandler().Handle(aue);
        if (result.Item2 is AskUser.AskUserError aue2) return await Handle(result.Item2);
        return result;
    }
}