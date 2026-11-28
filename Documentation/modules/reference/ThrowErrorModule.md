# ThrowErrorModule

Hint: `[throwerror]`  
Type: `PLang.Modules.ThrowErrorModule.Program`

Allows user to throw error or retry a step. Allows user to stop(end) running goal, leaving it early without sending anything back. Create payment request(status code 402). Not for a step that returns a value, e.g. `return %result%`: giving named values back to the caller is the Return method of VariableModule, whatever the values happen to hold. Not for a step that starts with `call` or `call goal`: that runs a goal, CallGoalModule, even when the goal is named ShowError, ThrowError or HandleError, e.g. `call goal ShowError message="Not found"`

## Methods

### CreatePaymentRequest

```
CreatePaymentRequest(String name, String description, String error, List<Dictionary<String, Object>> services = null) : PLang.Errors.Types.PaymentContract
```

Create payment request(402)


### EndApp

```
EndApp(Int32 exitCode = 0) : object
```

Shutdown the application. exitCode is the process exit code the step states, e.g. `end app, exit code 1`; 0 when the step gives none


### EndGoalExecution

```
EndGoalExecution(String message = null, Int32 levels = 0) : object
```

When user intends the execution of the goal to stop without giving a error response. This is equal to doing return in a function. levels is how many goals above this one also end: the number the step gives for goals above, whatever the wording (levels up, previous goals, last goals, goals above); `and previous` is 1


Examples:

- end goal => levels=0
- end goal and previous => levels=1
- end goal and 3 levels up => levels=3
- end goal and 2 last goals => levels=2

### Retry

```
Retry(Int32 maxRetries = 1, String maxRetriesReachedMesage = null, String key = MaxRetries, Int32 statusCode = 400, String fixSuggestion = null, String helpfullLinks = null) : object
```

Retries a step that caused an error. maxRetriesReachedMesage can contain {0} to include the retry count, when null a default message will be provided


### Throw

```
Throw(Object message, String key = UserDefinedError, Int32 statusCode = 400, String fixSuggestion = null, String helpfullLinks = null) : object
```

When user intends to throw an error or critical, etc. This can be stated as 'show error', 'throw crtical', 'print error', etc. Never for `call goal ShowError ...`: a goal named after an error is called, not thrown. type can be error|critical. statusCode(like http status code) should be defined by user. error is %!error% if user defines it


### ThrowError

```
ThrowError(PLang.Services.OutputStream.Messages.ErrorMessage errorMessage) : object
```

When user intends to throw an error or critical, etc. This can be stated as 'show error', 'throw crtical', 'print error', etc. Never for `call goal ShowError ...`: a goal named after an error is called, not thrown

- `errorMessage` *PLang.Services.OutputStream.Messages.ErrorMessage* — (see Type information in SupportingObjects)

