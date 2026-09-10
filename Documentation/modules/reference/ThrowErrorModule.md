# ThrowErrorModule

Hint: `[throwerror]`  
Type: `PLang.Modules.ThrowErrorModule.Program`

Allows user to throw error or retry a step. Allows user to return out of goal or stop(end) running goal. Create payment request(status code 402)

## Methods

### CreatePaymentRequest

```
CreatePaymentRequest(String name, String description, String error, List<Dictionary<String, Object>> services = null) : PLang.Errors.Types.PaymentContract
```

Create payment request(402)


### EndApp

```
EndApp() : object
```

Shutdown the application


### EndGoalExecution

```
EndGoalExecution(String message = null, Int32 levels = 0) : object
```

When user intends the execution of the goal to stop without giving a error response. This is equal to doing return in a function. Depth is how far up the stack it should end, previous goal is 1


### Retry

```
Retry(Int32 maxRetries = 1, String maxRetriesReachedMesage = null, String key = MaxRetries, Int32 statusCode = 400, String fixSuggestion = null, String helpfullLinks = null) : object
```

Retries a step that caused an error. maxRetriesReachedMesage can contain {0} to include the retry count, when null a default message will be provided


### Throw

```
Throw(Object message, String key = UserDefinedError, Int32 statusCode = 400, String fixSuggestion = null, String helpfullLinks = null) : object
```

When user intends to throw an error or critical, etc. This can be stated as 'show error', 'throw crtical', 'print error', etc. type can be error|critical. statusCode(like http status code) should be defined by user. error is %!error% if user defines it


### ThrowError

```
ThrowError(PLang.Services.OutputStream.Messages.ErrorMessage errorMessage) : object
```

When user intends to throw an error or critical, etc. This can be stated as 'show error', 'throw crtical', 'print error', etc.

- `errorMessage` *PLang.Services.OutputStream.Messages.ErrorMessage* — (see Type information in SupportingObjects)

